namespace ServerManager.Application.Interfaces.Security;

public interface ISecretProtector
{
    int KeyVersion { get; }

    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
