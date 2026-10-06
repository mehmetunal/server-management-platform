using System.Text;
using Renci.SshNet;

namespace ServerManager.E2E.Tests.Infrastructure;

/// <summary>
/// Testin kendi SSH kanalı: hazırlık (git deposu, örnek container) ve doğrulama (container/volume gerçekten silindi mi)
/// komutları uygulamadan bağımsız çalışır. Yalnızca E2E konteyneri içindir; host key doğrulanmaz.
/// </summary>
public sealed class TestShell : IDisposable
{
    private readonly SshClient _client;

    public TestShell(string host, int port, string user, string password)
    {
        _client = new SshClient(new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, password))
        {
            Timeout = TimeSpan.FromSeconds(30)
        });
        _client.Connect();
    }

    public sealed record Result(int ExitCode, string Output, string Error)
    {
        public override string ToString() => $"exit={ExitCode}\nstdout:\n{Output}\nstderr:\n{Error}";
    }

    public async Task<Result> RunAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        using var cmd = _client.CreateCommand(command);
        cmd.CommandTimeout = timeout ?? TimeSpan.FromMinutes(5);
        await cmd.ExecuteAsync(cancellationToken);
        return new Result(cmd.ExitStatus ?? -1, cmd.Result, cmd.Error);
    }

    /// <summary>Komut başarısız olursa çıktıyla birlikte testi düşürür.</summary>
    public async Task<string> RunCheckedAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(command, timeout, cancellationToken);
        Assert.True(result.ExitCode == 0, $"Komut başarısız: {command}\n{result}");
        return result.Output;
    }

    /// <summary>Komut hatasında test düşmez (temizlik adımları için).</summary>
    public async Task TryRunAsync(string command)
    {
        try
        {
            await RunAsync(command, TimeSpan.FromMinutes(2));
        }
        catch (Exception)
        {
            // Temizlik en iyi çabadır.
        }
    }

    public static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    /// <summary>Dosyayı base64 ile yazar (tırnak/kaçış sorunu olmadan).</summary>
    public Task<string> WriteFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
        RunCheckedAsync(
            $"mkdir -p {Quote(Path.GetDirectoryName(path)!.Replace('\\', '/'))} && echo {Convert.ToBase64String(Encoding.UTF8.GetBytes(content))} | base64 -d > {Quote(path)}",
            cancellationToken: cancellationToken);

    public void Dispose()
    {
        if (_client.IsConnected)
            _client.Disconnect();
        _client.Dispose();
    }
}
