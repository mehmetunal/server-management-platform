namespace ServerManager.Domain.Enums;

public enum TerminalSessionKind
{
    Server = 1,
    Container = 2,

    /// <summary>Panel servisinin (Servisler) container'ında şablonun istemci komutuyla açılan konsol.</summary>
    ServiceConsole = 3
}
