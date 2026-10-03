using System.Text;

namespace ServerManager.Application.Terminal;

/// <summary>
/// Terminale gönderilen tuş vuruşlarından, kabuğun o an düzenlediği komut satırını tahmin eder.
/// İmleç hareketi, Tab tamamlama ve geçmişten çağırma kabukta gerçekleştiği için izlenemez; bu durumda satır "yaklaşık" işaretlenir.
/// </summary>
public sealed class TerminalInputTracker
{
    public const int MaxLineLength = 4000;

    private const string BracketedPasteStart = "200~";
    private const string BracketedPasteEnd = "201~";

    private readonly StringBuilder _line = new();
    private readonly StringBuilder _escape = new();
    private EscapeState _escapeState;
    private bool _approximate;
    private bool _inPaste;

    private enum EscapeState
    {
        None,
        Escape,
        Csi,
        Ss3
    }

    public string CurrentLine => _line.ToString();

    /// <summary>Girdiyi ilk Enter'a kadar işler. Satır gönderilmezse tüm girdi tüketilir.</summary>
    public TerminalInputStep Next(ReadOnlySpan<char> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (Process(data[i]))
            {
                var submitted = new TerminalCommandLine(_line.ToString(), _approximate);
                Reset();
                return new TerminalInputStep(i + 1, submitted);
            }
        }

        return new TerminalInputStep(data.Length, null);
    }

    public void Reset()
    {
        _line.Clear();
        _escape.Clear();
        _escapeState = EscapeState.None;
        _approximate = false;
        _inPaste = false;
    }

    /// <returns>Satır gönderildiyse true.</returns>
    private bool Process(char c)
    {
        switch (_escapeState)
        {
            case EscapeState.Escape:
                ProcessEscape(c);
                return false;
            case EscapeState.Csi:
                ProcessCsi(c);
                return false;
            case EscapeState.Ss3:
                _escapeState = EscapeState.None;
                _approximate = true;
                return false;
        }

        switch (c)
        {
            case '\r' or '\n' when _inPaste:
                Append('\n');
                return false;
            case '\r' or '\n':
                return true;
            case '\u001b':
                _escapeState = EscapeState.Escape;
                return false;
            case '\u007f' or '\b':
                if (_line.Length > 0)
                    _line.Length--;
                return false;
            case '\u0003':
                Reset();
                return false;
            case '\u0015':
                _line.Clear();
                return false;
            case '\u0017':
                DeleteLastWord();
                return false;
            case '\u000c':
                return false;
            case '\t' when _inPaste:
                Append(c);
                return false;
        }

        if (char.IsControl(c))
        {
            _approximate = true;
            return false;
        }

        Append(c);
        return false;
    }

    private void ProcessEscape(char c)
    {
        switch (c)
        {
            case '[':
                _escape.Clear();
                _escapeState = EscapeState.Csi;
                break;
            case 'O':
                _escapeState = EscapeState.Ss3;
                break;
            default:
                // Alt+tuş kombinasyonları (kelime silme, geçmiş vb.) satırı kabukta değiştirir.
                _escapeState = EscapeState.None;
                _approximate = true;
                break;
        }
    }

    private void ProcessCsi(char c)
    {
        if (c is >= '\u0040' and <= '\u007e')
        {
            _escape.Append(c);
            var sequence = _escape.ToString();
            _escape.Clear();
            _escapeState = EscapeState.None;

            if (sequence == BracketedPasteStart)
                _inPaste = true;
            else if (sequence == BracketedPasteEnd)
                _inPaste = false;
            else
                _approximate = true;
            return;
        }

        if (_escape.Length < 32)
            _escape.Append(c);
    }

    private void Append(char c)
    {
        if (_line.Length < MaxLineLength)
            _line.Append(c);
        else
            _approximate = true;
    }

    private void DeleteLastWord()
    {
        var end = _line.Length;
        while (end > 0 && _line[end - 1] == ' ')
            end--;
        while (end > 0 && _line[end - 1] != ' ')
            end--;
        _line.Length = end;
    }
}
