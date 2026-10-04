using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Validators.Servers;

public abstract partial class ServerFormDtoValidator<T> : AbstractValidator<T> where T : ServerFormDto
{
    protected ServerFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sunucu adı zorunludur.")
            .MaximumLength(128).WithMessage("Sunucu adı en fazla 128 karakter olabilir.")
            .Matches(NamePattern()).WithMessage("Sunucu adı yalnızca harf, rakam, boşluk, nokta, alt çizgi ve tire içerebilir.");

        RuleFor(x => x.Hostname)
            .NotEmpty().WithMessage("Hostname zorunludur.")
            .MaximumLength(255).WithMessage("Hostname en fazla 255 karakter olabilir.")
            .Matches(HostnamePattern()).WithMessage("Geçerli bir hostname girin (ör. web-01.example.com).");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP adresi zorunludur.")
            .MaximumLength(45).WithMessage("IP adresi en fazla 45 karakter olabilir.")
            .Must(ip => IPAddress.TryParse(ip, out _)).WithMessage("Geçerli bir IPv4 veya IPv6 adresi girin.");

        RuleFor(x => x.SshPort)
            .InclusiveBetween(1, 65535).WithMessage("SSH portu 1 ile 65535 arasında olmalıdır.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Kullanıcı adı zorunludur.")
            .MaximumLength(64).WithMessage("Kullanıcı adı en fazla 64 karakter olabilir.")
            .Matches(UsernamePattern()).WithMessage("Geçerli bir Linux kullanıcı adı girin.");

        RuleFor(x => x.AuthenticationType)
            .IsInEnum().WithMessage("Geçerli bir kimlik doğrulama yöntemi seçin.");

        RuleFor(x => x.Environment)
            .IsInEnum().WithMessage("Geçerli bir ortam seçin.");

        RuleFor(x => x.Password)
            .MaximumLength(1024).WithMessage("SSH parolası en fazla 1024 karakter olabilir.");

        RuleFor(x => x.PrivateKey)
            .MaximumLength(16384).WithMessage("Private key en fazla 16384 karakter olabilir.")
            .Must(BeAPrivateKey).WithMessage("Private key OpenSSH/PEM formatında olmalıdır (-----BEGIN ... PRIVATE KEY-----).")
            .When(x => !string.IsNullOrWhiteSpace(x.PrivateKey));

        RuleFor(x => x.Passphrase)
            .MaximumLength(1024).WithMessage("Passphrase en fazla 1024 karakter olabilir.");

        RuleFor(x => x.SudoPassword)
            .MaximumLength(1024).WithMessage("Sudo parolası en fazla 1024 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Açıklama en fazla 1000 karakter olabilir.");

        RuleFor(x => x.Location)
            .MaximumLength(128).WithMessage("Lokasyon en fazla 128 karakter olabilir.");

        RuleFor(x => x.Provider)
            .MaximumLength(128).WithMessage("Provider en fazla 128 karakter olabilir.");

        RuleFor(x => x.MonthlyCost)
            .InclusiveBetween(0m, 1_000_000m).When(x => x.MonthlyCost.HasValue).WithMessage("Aylık maliyet 0 ile 1.000.000 arasında olmalıdır.");

        RuleFor(x => x.CostCurrency)
            .Must(c => string.IsNullOrWhiteSpace(c) || CostCurrencies.IsValid(c.Trim().ToUpperInvariant())).WithMessage("Geçerli bir para birimi seçin.");

        RuleFor(x => x.OperatingSystem)
            .MaximumLength(128).WithMessage("İşletim sistemi en fazla 128 karakter olabilir.");

        RuleFor(x => x.Tags)
            .Must(tags => TagParser.Parse(tags).Count <= TagParser.MaxTagCount)
            .WithMessage($"En fazla {TagParser.MaxTagCount} etiket girilebilir.")
            .Must(tags => TagParser.Parse(tags).All(t => t.Length <= TagParser.MaxTagLength && TagPattern().IsMatch(t)))
            .WithMessage("Etiketler küçük harf, rakam, nokta, alt çizgi veya tire içerebilir (ör. production, web, eu).");
    }

    private static bool BeAPrivateKey(string? key) =>
        key is not null
        && key.Contains("-----BEGIN", StringComparison.Ordinal)
        && key.Contains("PRIVATE KEY-----", StringComparison.Ordinal);

    [GeneratedRegex(@"^[\p{L}\p{N} ._\-]+$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^(?=.{1,255}$)[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$")]
    private static partial Regex HostnamePattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.\-]{0,63}$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_.\-]*$")]
    private static partial Regex TagPattern();
}
