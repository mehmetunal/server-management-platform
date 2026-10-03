using FluentValidation;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Servers;

public sealed class CreateServerDtoValidator : ServerFormDtoValidator<CreateServerDto>
{
    public CreateServerDtoValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola ile kimlik doğrulamada SSH parolası zorunludur.")
            .When(x => x.AuthenticationType == AuthenticationType.Password);

        RuleFor(x => x.PrivateKey)
            .NotEmpty().WithMessage("Private key ile kimlik doğrulamada private key zorunludur.")
            .When(x => x.AuthenticationType is AuthenticationType.PrivateKey or AuthenticationType.PrivateKeyWithPassphrase);

        RuleFor(x => x.Passphrase)
            .NotEmpty().WithMessage("Passphrase korumalı key için passphrase zorunludur.")
            .When(x => x.AuthenticationType == AuthenticationType.PrivateKeyWithPassphrase);
    }
}
