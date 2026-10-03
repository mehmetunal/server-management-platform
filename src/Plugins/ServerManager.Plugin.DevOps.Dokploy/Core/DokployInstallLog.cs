using System.Text;

namespace ServerManager.Plugin.DevOps.Dokploy.Core;

/// <summary>Kurulum çıktısını sınırlı boyutta tutar; sınır aşılırsa en eski kısım atılır.</summary>
public sealed class DokployInstallLog
{
    private const string TruncatedNotice = "[… çıktının başı kısaltıldı …]\r\n";

    private readonly StringBuilder _builder = new();
    private readonly Lock _gate = new();
    private readonly int _maxChars;
    private bool _truncated;

    public DokployInstallLog(int maxChars)
    {
        _maxChars = Math.Max(1024, maxChars);
    }

    public void Append(string text)
    {
        lock (_gate)
        {
            _builder.Append(text);
            if (_builder.Length > _maxChars)
            {
                _builder.Remove(0, _builder.Length - _maxChars);
                _truncated = true;
            }
        }
    }

    public override string ToString()
    {
        lock (_gate)
        {
            return _truncated ? TruncatedNotice + _builder : _builder.ToString();
        }
    }
}
