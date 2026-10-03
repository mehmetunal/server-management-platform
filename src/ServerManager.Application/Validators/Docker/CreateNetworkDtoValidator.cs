using FluentValidation;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Application.Validators.Docker;

public class CreateNetworkDtoValidator : AbstractValidator<CreateNetworkDto>
{
    public CreateNetworkDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Network adı zorunludur.")
            .MaximumLength(DockerNames.MaxLength).WithMessage($"Network adı en fazla {DockerNames.MaxLength} karakter olabilir.")
            .Must(DockerNames.IsValidNetworkName)
            .WithMessage("Network adı harf veya rakamla başlamalı; yalnızca harf, rakam, alt çizgi, nokta ve tire içerebilir (en az 2 karakter).")
            .Must(name => name is not ("bridge" or "host" or "none")).WithMessage("Bu ad Docker tarafından ayrılmıştır.");

        RuleFor(x => x.Driver)
            .Must(DockerNames.IsValidNetworkDriver)
            .WithMessage($"Driver şunlardan biri olmalıdır: {string.Join(", ", DockerNames.NetworkDrivers)}.");

        RuleFor(x => x.Subnet)
            .Must(DockerNames.IsValidSubnet).When(x => !string.IsNullOrWhiteSpace(x.Subnet))
            .WithMessage("Subnet CIDR biçiminde olmalıdır. Örnek: 172.30.0.0/16");

        RuleFor(x => x.Gateway)
            .Must(DockerNames.IsValidIpv4).When(x => !string.IsNullOrWhiteSpace(x.Gateway))
            .WithMessage("Gateway geçerli bir IPv4 adresi olmalıdır.");

        RuleFor(x => x.Subnet)
            .NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.Gateway))
            .WithMessage("Gateway belirtildiğinde subnet de belirtilmelidir.");
    }
}
