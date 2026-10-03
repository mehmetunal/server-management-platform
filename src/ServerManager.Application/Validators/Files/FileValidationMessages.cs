namespace ServerManager.Application.Validators.Files;

internal static class FileValidationMessages
{
    public const string InvalidPath = "Geçerli bir mutlak yol girin (ör. /var/www).";
    public const string InvalidName = "Ad boş olamaz, '/' içeremez ve en fazla 255 karakter olabilir.";
}
