using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServerManager.Application.Deployments;

public enum WebhookSource
{
    Unknown,
    GitHub,
    GitLab
}

/// <summary>Webhook isteğinden okunan başlıklar ve ham gövde; imza gövdenin ham baytları üzerinden doğrulanır.</summary>
public sealed record WebhookDelivery(
    byte[] Body,
    string? GitHubEvent = null,
    string? GitHubSignature256 = null,
    string? GitLabEvent = null,
    string? GitLabToken = null,
    string? DeliveryId = null,
    string? RemoteIp = null)
{
    public WebhookSource Source =>
        !string.IsNullOrEmpty(GitHubEvent) ? WebhookSource.GitHub
        : !string.IsNullOrEmpty(GitLabEvent) || !string.IsNullOrEmpty(GitLabToken) ? WebhookSource.GitLab
        : WebhookSource.Unknown;
}

/// <summary>Push olayından deploy için gereken alanlar.</summary>
public sealed record WebhookPush(string Ref, string? After, string? Pusher, bool Deleted);

public enum WebhookDecisionKind
{
    /// <summary>GitHub'ın kurulumda gönderdiği ping; 200 ile cevaplanır.</summary>
    Ping,
    Ignore,
    Deploy
}

public sealed record WebhookDecision(WebhookDecisionKind Kind, string Message, string? Commit = null, string? Pusher = null);

/// <summary>
/// Proje webhook'u: GitHub <c>X-Hub-Signature-256</c> (HMAC-SHA256) ve GitLab <c>X-Gitlab-Token</c> doğrulaması,
/// push olayının ayrıştırılması ve yalnızca proje dalına gelen push'un deploy edilmesi kararı.
/// </summary>
public static class ProjectWebhooks
{
    public const string GitHubEventHeader = "X-GitHub-Event";
    public const string GitHubSignatureHeader = "X-Hub-Signature-256";
    public const string GitHubDeliveryHeader = "X-GitHub-Delivery";
    public const string GitLabEventHeader = "X-Gitlab-Event";
    public const string GitLabTokenHeader = "X-Gitlab-Token";
    public const string GitLabDeliveryHeader = "X-Gitlab-Event-UUID";

    /// <summary>Webhook ile başlayan deployment'ın kayıt ve audit'teki kullanıcı adı.</summary>
    public const string ActorName = "webhook";

    public const int SecretBytes = 32;
    public const int MaxBodyBytes = 5 * 1024 * 1024;

    private const string SignaturePrefix = "sha256=";
    private const string ZeroSha = "0000000000000000000000000000000000000000";

    /// <summary>64 karakter hex gizli anahtar; GitHub ve GitLab alanlarına olduğu gibi yapıştırılır.</summary>
    public static string GenerateSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>İmza başlığı <c>sha256=&lt;hex&gt;</c> biçiminde olmalı; karşılaştırma sabit zamanlıdır.</summary>
    public static bool VerifyGitHubSignature(string secret, ReadOnlySpan<byte> body, string? header)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header) || !header.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(header.AsSpan(SignaturePrefix.Length).Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    /// <summary>GitLab belirteci düz metin gelir; uzunluk sızmasın diye iki tarafın SHA-256 özetleri sabit zamanda karşılaştırılır.</summary>
    public static bool VerifyGitLabToken(string secret, string? token)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(token))
            return false;

        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var provided = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    public static bool Verify(string secret, WebhookDelivery delivery) => delivery.Source switch
    {
        WebhookSource.GitHub => VerifyGitHubSignature(secret, delivery.Body, delivery.GitHubSignature256),
        WebhookSource.GitLab => VerifyGitLabToken(secret, delivery.GitLabToken),
        _ => false
    };

    public static bool TryParsePush(WebhookSource source, ReadOnlySpan<byte> body, out WebhookPush? push)
    {
        push = null;
        try
        {
            var reader = new Utf8JsonReader(body);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var reference = GetString(root, "ref");
            if (string.IsNullOrEmpty(reference))
                return false;

            var after = GetString(root, "after");
            if (source == WebhookSource.GitLab)
            {
                after = GetString(root, "checkout_sha") ?? after;
                var pusher = GetString(root, "user_username") ?? GetString(root, "user_name");
                push = new WebhookPush(reference, after, pusher, IsZeroSha(GetString(root, "after")));
                return true;
            }

            var deleted = root.TryGetProperty("deleted", out var deletedValue) && deletedValue.ValueKind == JsonValueKind.True;
            var name = root.TryGetProperty("pusher", out var pusherValue) && pusherValue.ValueKind == JsonValueKind.Object ? GetString(pusherValue, "name") : null;
            push = new WebhookPush(reference, after, name, deleted || IsZeroSha(after));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// İmzası doğrulanmış teslimat için karar: push dışındaki olaylar ve proje dalı dışındaki dallar yok sayılır;
    /// dal silme push'u deploy edilmez. Deploy kararında commit push'taki son commit'tir (geçerli SHA değilse dalın son hali).
    /// </summary>
    public static WebhookDecision Decide(WebhookDelivery delivery, string branch)
    {
        switch (delivery.Source)
        {
            case WebhookSource.GitHub when string.Equals(delivery.GitHubEvent, "ping", StringComparison.OrdinalIgnoreCase):
                return new WebhookDecision(WebhookDecisionKind.Ping, "Ping alındı; webhook bağlantısı çalışıyor.");
            case WebhookSource.GitHub when !string.Equals(delivery.GitHubEvent, "push", StringComparison.OrdinalIgnoreCase):
                return new WebhookDecision(WebhookDecisionKind.Ignore, $"'{Clip(delivery.GitHubEvent)}' olayı yok sayıldı; yalnızca push olayı deploy başlatır.");
            case WebhookSource.GitLab when !string.Equals(delivery.GitLabEvent, "Push Hook", StringComparison.OrdinalIgnoreCase):
                return new WebhookDecision(WebhookDecisionKind.Ignore, $"'{Clip(delivery.GitLabEvent)}' olayı yok sayıldı; yalnızca Push Hook deploy başlatır.");
            case WebhookSource.Unknown:
                return new WebhookDecision(WebhookDecisionKind.Ignore, "Webhook kaynağı tanınmadı.");
        }

        if (!TryParsePush(delivery.Source, delivery.Body, out var push) || push is null)
            return new WebhookDecision(WebhookDecisionKind.Ignore, "Push içeriği okunamadı.");

        var expected = "refs/heads/" + branch;
        if (!string.Equals(push.Ref, expected, StringComparison.Ordinal))
            return new WebhookDecision(WebhookDecisionKind.Ignore, $"{Clip(push.Ref)} yok sayıldı; proje dalı {branch}.");

        if (push.Deleted)
            return new WebhookDecision(WebhookDecisionKind.Ignore, $"{branch} dalı silindi; deploy edilmedi.");

        var commit = GitRefs.NormalizeCommit(push.After);
        return new WebhookDecision(WebhookDecisionKind.Deploy, $"{branch} dalına push", GitRefs.IsValidCommit(commit) ? commit : null, Clip(push.Pusher));
    }

    private static bool IsZeroSha(string? sha) => string.Equals(sha, ZeroSha, StringComparison.Ordinal);

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Clip(string? value) => value is null ? null : value.Length <= 100 ? value : value[..100];
}
