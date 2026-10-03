using Microsoft.AspNetCore.Identity;

namespace ServerManager.Infrastructure.Identity;

public class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Beklenmeyen bir hata oluştu." };

    public override IdentityError ConcurrencyFailure() =>
        new() { Code = nameof(ConcurrencyFailure), Description = "Kayıt başka bir işlem tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Parola hatalı." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Geçersiz doğrulama kodu." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = $"'{userName}' geçerli bir kullanıcı adı değil." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = $"'{email}' geçerli bir e-posta adresi değil." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"'{userName}' kullanıcı adı zaten kullanılıyor." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"'{email}' e-posta adresi zaten kullanılıyor." };

    public override IdentityError InvalidRoleName(string? role) =>
        new() { Code = nameof(InvalidRoleName), Description = $"'{role}' geçerli bir rol adı değil." };

    public override IdentityError DuplicateRoleName(string role) =>
        new() { Code = nameof(DuplicateRoleName), Description = $"'{role}' rolü zaten var." };

    public override IdentityError UserAlreadyInRole(string role) =>
        new() { Code = nameof(UserAlreadyInRole), Description = $"Kullanıcı zaten '{role}' rolünde." };

    public override IdentityError UserNotInRole(string role) =>
        new() { Code = nameof(UserNotInRole), Description = $"Kullanıcı '{role}' rolünde değil." };

    public override IdentityError UserLockoutNotEnabled() =>
        new() { Code = nameof(UserLockoutNotEnabled), Description = "Bu kullanıcı için kilitleme etkin değil." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Parola en az {length} karakter olmalıdır." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Parola en az {uniqueChars} farklı karakter içermelidir." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Parola en az bir özel karakter içermelidir." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Parola en az bir rakam içermelidir." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Parola en az bir küçük harf içermelidir." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Parola en az bir büyük harf içermelidir." };
}
