using System.Net.Sockets;
using System.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ServerManager.Application;
using ServerManager.Application.Alerting;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Notifications;
using ServerManager.Infrastructure.Monitoring;

namespace ServerManager.Plugin.Notifications.Email;

public sealed class EmailNotificationProvider : INotificationChannelProvider
{
    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(EmailPlugin.HostKey, "SMTP sunucusu", Placeholder: "smtp.example.com", MaxLength: 253),
        new(EmailPlugin.PortKey, "Port", NotificationFieldType.Number, DefaultValue: "587",
            Hint: "STARTTLS için genellikle 587, SSL için 465.", MaxLength: 5),
        new(EmailPlugin.SecurityKey, "Bağlantı güvenliği", NotificationFieldType.Select, DefaultValue: EmailPlugin.SecurityStartTls,
            Options:
            [
                new(EmailPlugin.SecurityStartTls, "STARTTLS (önerilen)"),
                new(EmailPlugin.SecuritySsl, "SSL/TLS (465)"),
                new(EmailPlugin.SecurityNone, "Şifrelemesiz (yalnızca iç ağ)")
            ]),
        new(EmailPlugin.UsernameKey, "Kullanıcı adı", IsRequired: false, Hint: "Kimlik doğrulama gerekmiyorsa boş bırakın.", MaxLength: 200),
        new(EmailPlugin.PasswordKey, "Parola", NotificationFieldType.Secret, IsRequired: false,
            Hint: "Kaydedildikten sonra gösterilmez; değiştirmek istemiyorsanız boş bırakın.", MaxLength: 200),
        new(EmailPlugin.FromKey, "Gönderen adresi", NotificationFieldType.Email, Placeholder: "alarm@example.com", MaxLength: 200),
        new(EmailPlugin.ToKey, "Alıcılar", Placeholder: "ops@example.com, admin@example.com",
            Hint: $"Virgülle ayırarak en fazla {EmailPlugin.MaxRecipients} adres.", MaxLength: 1000)
    ];

    public string SystemName => EmailPlugin.SystemName;

    public string DisplayName => EmailPlugin.DisplayName;

    public string Description => "SMTP sunucusu üzerinden e-posta gönderir.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        var errors = new List<ServiceError>();
        if (settings.TryGetValue(EmailPlugin.HostKey, out var host) && !NetworkTargets.IsValidHost(host))
            errors.Add(new ServiceError(EmailPlugin.HostKey, "Geçerli bir sunucu adı veya IP adresi girin."));

        if (settings.TryGetValue(EmailPlugin.PortKey, out var port) && !(int.TryParse(port, out var value) && value is >= 1 and <= 65535))
            errors.Add(new ServiceError(EmailPlugin.PortKey, "Port 1-65535 arasında olmalıdır."));

        if (settings.TryGetValue(EmailPlugin.FromKey, out var from) && !IsValidAddress(from))
            errors.Add(new ServiceError(EmailPlugin.FromKey, "Geçerli bir gönderen adresi girin."));

        if (settings.TryGetValue(EmailPlugin.ToKey, out var to))
        {
            var recipients = SplitRecipients(to);
            if (recipients.Count == 0 || recipients.Count > EmailPlugin.MaxRecipients)
                errors.Add(new ServiceError(EmailPlugin.ToKey, $"1-{EmailPlugin.MaxRecipients} arası alıcı adresi girin."));
            else if (recipients.FirstOrDefault(r => !IsValidAddress(r)) is { } invalid)
                errors.Add(new ServiceError(EmailPlugin.ToKey, $"Geçersiz alıcı adresi: {invalid}"));
        }

        var hasUser = settings.TryGetValue(EmailPlugin.UsernameKey, out var user) && !string.IsNullOrWhiteSpace(user);
        var hasPassword = settings.TryGetValue(EmailPlugin.PasswordKey, out var password) && !string.IsNullOrEmpty(password);
        if (hasPassword && !hasUser)
            errors.Add(new ServiceError(EmailPlugin.UsernameKey, "Parola girildiyse kullanıcı adı da gereklidir."));

        return errors;
    }

    public async Task<ServiceResult> SendAsync(IReadOnlyDictionary<string, string> settings, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (Validate(settings).Count > 0
            || !settings.TryGetValue(EmailPlugin.HostKey, out var host)
            || !settings.TryGetValue(EmailPlugin.FromKey, out var from)
            || !settings.TryGetValue(EmailPlugin.ToKey, out var to))
        {
            return ServiceResult.Failure("E-posta ayarları eksik veya geçersiz.");
        }

        var port = settings.TryGetValue(EmailPlugin.PortKey, out var portText) && int.TryParse(portText, out var parsed) ? parsed : 587;
        var security = settings.GetValueOrDefault(EmailPlugin.SecurityKey) switch
        {
            EmailPlugin.SecuritySsl => SecureSocketOptions.SslOnConnect,
            EmailPlugin.SecurityNone => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls
        };

        var mail = BuildMessage(message, from, SplitRecipients(to));

        try
        {
            using var client = new SmtpClient();
            var socket = await NetworkTargetGuard.ConnectAsync(host, port, cancellationToken);
            try
            {
                await client.ConnectAsync(socket, host, port, security, cancellationToken);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            if (settings.TryGetValue(EmailPlugin.UsernameKey, out var user) && !string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, settings.GetValueOrDefault(EmailPlugin.PasswordKey) ?? string.Empty, cancellationToken);

            await client.SendAsync(mail, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return ServiceResult.Success("E-posta gönderildi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ServiceResult.Failure(Describe(ex));
        }
    }

    public static MimeMessage BuildMessage(NotificationMessage message, string from, IReadOnlyList<string> recipients)
    {
        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(from));
        foreach (var recipient in recipients)
            mail.To.Add(MailboxAddress.Parse(recipient));

        mail.Subject = message.Title;
        mail.Date = DateTime.SpecifyKind(message.OccurredAt, DateTimeKind.Utc);

        var body = new StringBuilder(message.Body);
        if (message.Url is not null)
            body.Append("\n\nPanelde aç: ").Append(message.Url);
        body.Append("\n\n— ").Append(ProductInfo.Name);
        mail.Body = new TextPart("plain") { Text = body.ToString() };
        return mail;
    }

    // MimeKit alan adı olmayan "kullanici" yazımını da geçerli sayar.
    public static bool IsValidAddress(string value) =>
        MailboxAddress.TryParse(value, out var mailbox)
        && !string.IsNullOrEmpty(mailbox.LocalPart)
        && !string.IsNullOrEmpty(mailbox.Domain)
        && NetworkTargets.IsValidHost(mailbox.Domain);

    public static IReadOnlyList<string> SplitRecipients(string value) =>
        value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Describe(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case BlockedNetworkTargetException:
                    return current.Message;
                case AuthenticationException:
                    return "SMTP kimlik doğrulaması başarısız (kullanıcı adı/parola).";
                case SslHandshakeException:
                    return "SMTP TLS el sıkışması başarısız (güvenlik ayarını veya portu kontrol edin).";
                case SmtpCommandException command:
                    return $"SMTP sunucusu isteği reddetti ({(int)command.StatusCode}).";
                case SmtpProtocolException:
                    return "SMTP protokol hatası.";
                case ServiceNotConnectedException:
                    return "SMTP bağlantısı kurulamadı.";
                case SocketException socket:
                    return socket.SocketErrorCode switch
                    {
                        SocketError.ConnectionRefused => "SMTP sunucusu bağlantıyı reddetti (port kapalı).",
                        SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => "SMTP sunucu adı çözümlenemedi.",
                        _ => "SMTP sunucusuna bağlanılamadı."
                    };
                case NotSupportedException when current.Message.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase):
                    return "SMTP sunucusu STARTTLS desteklemiyor.";
            }
        }

        return "E-posta gönderilemedi.";
    }
}
