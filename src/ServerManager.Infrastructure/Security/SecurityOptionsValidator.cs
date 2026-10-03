using Microsoft.Extensions.Options;

namespace ServerManager.Infrastructure.Security;

public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public const int RequiredKeyLength = 32;

    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MasterKey))
            return ValidateOptionsResult.Fail(
                "Security:MasterKey tanımlı değil. Base64 kodlu 32 baytlık bir anahtarı ortam değişkeni (Security__MasterKey) veya user-secrets ile verin.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.MasterKey);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Security:MasterKey geçerli bir Base64 değeri değil.");
        }

        if (key.Length != RequiredKeyLength)
            return ValidateOptionsResult.Fail($"Security:MasterKey {RequiredKeyLength} bayt (AES-256) olmalıdır; verilen anahtar {key.Length} bayt.");

        if (options.KeyVersion < 1)
            return ValidateOptionsResult.Fail("Security:KeyVersion 1 veya daha büyük olmalıdır.");

        return ValidateOptionsResult.Success;
    }
}
