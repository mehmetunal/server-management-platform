namespace ServerManager.Application.Terminal;

/// <param name="Consumed">İşlenen karakter sayısı. Satır gönderildiyse son karakter Enter'dır.</param>
/// <param name="Submitted">Enter'a basıldıysa gönderilen satır; aksi halde null.</param>
public readonly record struct TerminalInputStep(int Consumed, TerminalCommandLine? Submitted);
