namespace ServerManager.Infrastructure.Ssh;

/// <summary>
/// Etkileşimli terminalde sudo prompt işaretini yakalar ve parolayı bir kez yazdırır. İşaret yalnızca sudo çağrısından sonraki
/// kısa pencerede kabul edilir; parola gönderildikten veya pencere geçtikten sonra unutulur. Böylece sonradan çalışan bir
/// programın (container çıktısı vb.) işareti basması sunucunun sudo parolasını terminale yazdırmaz.
/// </summary>
internal sealed class SudoPromptResponder
{
    public static readonly TimeSpan PromptWindow = TimeSpan.FromSeconds(15);

    private const string Marker = SudoCommandBuilder.TerminalPromptMarker;

    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _deadline;
    private string? _password;
    private bool _active;
    private string _pending = string.Empty;

    public SudoPromptResponder(string? password, TimeProvider timeProvider)
    {
        _password = string.IsNullOrEmpty(password) ? null : password;
        _active = _password is not null;
        _timeProvider = timeProvider;
        _deadline = timeProvider.GetUtcNow() + PromptWindow;
    }

    /// <param name="text">Terminalden okunan parça.</param>
    /// <param name="reply">PTY'ye yazılacak parola satırı; yoksa null.</param>
    /// <returns>Kullanıcıya iletilecek metin; sudo parolayı pencere içinde yeniden sorduysa (parola hatalı) null.</returns>
    public string? Process(string text, out string? reply)
    {
        reply = null;
        var buffer = _pending + text;
        _pending = string.Empty;
        if (!_active)
            return buffer;

        if (_timeProvider.GetUtcNow() >= _deadline)
        {
            // Pencere dışında görülen işaret komutun kendi çıktısıdır; parola artık hiçbir koşulda yazılmaz.
            _active = false;
            _password = null;
            return buffer;
        }

        var index = buffer.IndexOf(Marker, StringComparison.Ordinal);
        if (index < 0)
        {
            // İşaret iki okuma arasında bölünebilir; parola gönderilene kadar olası ön eki bir sonraki parçaya bekletir.
            if (_password is not null)
            {
                var keep = PartialMarkerSuffixLength(buffer);
                _pending = buffer[^keep..];
                return buffer[..^keep];
            }

            return buffer;
        }

        if (_password is null)
            return null;

        reply = _password + "\n";
        _password = null;
        return buffer.Remove(index, Marker.Length);
    }

    private static int PartialMarkerSuffixLength(string buffer)
    {
        for (var length = Math.Min(Marker.Length - 1, buffer.Length); length > 0; length--)
        {
            if (Marker.AsSpan().StartsWith(buffer.AsSpan(buffer.Length - length), StringComparison.Ordinal))
                return length;
        }

        return 0;
    }
}
