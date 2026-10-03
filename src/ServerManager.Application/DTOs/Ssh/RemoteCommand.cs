namespace ServerManager.Application.DTOs.Ssh;

/// <param name="CommandText">Tek bir program çağrısı olmalıdır; Elevate açıkken sudo yalnızca ilk programa uygulanır.</param>
/// <param name="StandardInput">
/// Komutun stdin'ine yazılır (sudo parolasından sonra). Gizli değerler komut satırına değil buraya konur;
/// böylece süreç listesinde ve kabuk geçmişinde görünmez.
/// </param>
public sealed record RemoteCommand(string CommandText, TimeSpan Timeout, bool Elevate = false, string? StandardInput = null)
{
    public override string ToString() =>
        $"RemoteCommand {{ CommandText = {CommandText}, Timeout = {Timeout}, Elevate = {Elevate}, StandardInput = {(StandardInput is null ? "null" : "***")} }}";
}
