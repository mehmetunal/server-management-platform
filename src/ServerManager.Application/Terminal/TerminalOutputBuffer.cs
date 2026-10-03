using System.Text;

namespace ServerManager.Application.Terminal;

/// <summary>Yeniden bağlanan istemciye ekranı geri yüklemek için son çıktıyı sınırlı boyutta tutar.</summary>
public sealed class TerminalOutputBuffer
{
    private readonly StringBuilder _buffer = new();
    private readonly Lock _gate = new();
    private readonly int _capacity;
    private bool _truncated;

    public TerminalOutputBuffer(int capacity)
    {
        _capacity = Math.Max(1024, capacity);
    }

    public void Append(string data)
    {
        if (string.IsNullOrEmpty(data))
            return;

        lock (_gate)
        {
            _buffer.Append(data);
            if (_buffer.Length <= _capacity)
                return;

            var excess = _buffer.Length - _capacity;
            // Kesme noktası bir satır başına kaydırılır; böylece yarım kalmış kaçış dizisi ekrana basılmaz.
            var newline = IndexOf(_buffer, '\n', excess, Math.Min(_buffer.Length, excess + 4096));
            _buffer.Remove(0, newline >= 0 ? newline + 1 : excess);
            _truncated = true;
        }
    }

    public string Snapshot()
    {
        lock (_gate)
            return _truncated ? "\u001b[0m" + _buffer : _buffer.ToString();
    }

    private static int IndexOf(StringBuilder builder, char value, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (builder[i] == value)
                return i;
        }

        return -1;
    }
}
