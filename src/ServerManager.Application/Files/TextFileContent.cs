namespace ServerManager.Application.Files;

/// <param name="LineEnding"><see cref="TextFileCodec.Lf"/> veya <see cref="TextFileCodec.Crlf"/>.</param>
public sealed record TextFileContent(string Content, bool HasBom, string LineEnding);
