using System.Text;

namespace ServerManager.Infrastructure.Ssh;

/// <summary>Akışlı komut çıktısının yalnızca son kısmını tutar.</summary>
internal sealed class OutputTail
{
    private const int Capacity = 16_384;

    private readonly StringBuilder _builder = new();
    private readonly Lock _gate = new();

    public void Append(string text)
    {
        lock (_gate)
        {
            _builder.Append(text);
            if (_builder.Length > Capacity * 2)
                _builder.Remove(0, _builder.Length - Capacity);
        }
    }

    public override string ToString()
    {
        lock (_gate)
        {
            return _builder.Length <= Capacity ? _builder.ToString() : _builder.ToString(_builder.Length - Capacity, Capacity);
        }
    }
}
