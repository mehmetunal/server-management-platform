using System.Threading.Channels;
using ServerManager.Application.DTOs.Terminal;

namespace ServerManager.Application.Terminal;

/// <summary>Terminal komutları tuş vuruşu yolunu bekletmemek için arka planda toplu yazılır.</summary>
public sealed class TerminalCommandQueue
{
    public const int Capacity = 10_000;

    private readonly Channel<TerminalCommandEntry> _channel = Channel.CreateBounded<TerminalCommandEntry>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public ChannelReader<TerminalCommandEntry> Reader => _channel.Reader;

    /// <returns>Kuyruk doluysa false.</returns>
    public bool TryEnqueue(TerminalCommandEntry entry) => _channel.Writer.TryWrite(entry);

    public void Complete() => _channel.Writer.TryComplete();
}
