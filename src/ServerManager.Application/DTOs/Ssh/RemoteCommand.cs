namespace ServerManager.Application.DTOs.Ssh;

/// <param name="CommandText">Tek bir program çağrısı olmalıdır; Elevate açıkken sudo yalnızca ilk programa uygulanır.</param>
public sealed record RemoteCommand(string CommandText, TimeSpan Timeout, bool Elevate = false);
