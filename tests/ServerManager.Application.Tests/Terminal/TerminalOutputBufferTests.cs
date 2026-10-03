using ServerManager.Application.Terminal;

namespace ServerManager.Application.Tests.Terminal;

public class TerminalOutputBufferTests
{
    [Fact]
    public void Returns_all_output_while_under_capacity()
    {
        var buffer = new TerminalOutputBuffer(4096);
        buffer.Append("hello ");
        buffer.Append("world");

        Assert.Equal("hello world", buffer.Snapshot());
    }

    [Fact]
    public void Drops_oldest_output_at_line_boundary_and_resets_attributes()
    {
        var buffer = new TerminalOutputBuffer(1024);
        for (var i = 0; i < 100; i++)
            buffer.Append($"line-{i:D3} {new string('x', 20)}\n");

        var snapshot = buffer.Snapshot();

        Assert.StartsWith("\u001b[0mline-", snapshot);
        Assert.EndsWith($"line-099 {new string('x', 20)}\n", snapshot);
        Assert.True(snapshot.Length <= 1024 + 4);
    }

    [Fact]
    public void Capacity_has_lower_bound()
    {
        var buffer = new TerminalOutputBuffer(10);
        buffer.Append(new string('a', 1000));

        Assert.Equal(1000, buffer.Snapshot().Length);
    }
}
