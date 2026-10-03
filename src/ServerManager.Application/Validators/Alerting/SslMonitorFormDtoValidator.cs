using FluentValidation;
using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Ssl;

namespace ServerManager.Application.Validators.Alerting;

public sealed class SslMonitorFormDtoValidator : AbstractValidator<SslMonitorFormDto>
{
    public SslMonitorFormDtoValidator()
    {
        RuleFor(x => x.Host)
            .Must(NetworkTargets.IsValidHost).WithMessage("Geçerli bir alan adı veya IP adresi girin (ör. panel.firma.com).");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Port 1 ile 65535 arasında olmalıdır.");
    }
}
