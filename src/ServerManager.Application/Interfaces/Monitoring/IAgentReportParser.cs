using ServerManager.Application.DTOs.Monitoring;

namespace ServerManager.Application.Interfaces.Monitoring;

/// <summary>Agent'ın sunucuda çalıştırdığı ölçüm betiği ve çıktısının ayrıştırıcısı (SSH toplayıcısıyla aynı betik).</summary>
public interface IAgentReportParser
{
    string CollectionScript { get; }

    /// <exception cref="FormatException">Çıktı eksik veya okunamazsa.</exception>
    SystemMetricsSnapshot Parse(string output, DateTime nowUtc);
}
