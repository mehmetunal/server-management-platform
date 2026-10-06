using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary>Kaynak geçmişi betikleri. Bölümler <c>@@rh:ad</c> satırlarıyla ayrılır; <c>@@rh:end</c> çıktının tamamlandığını gösterir.</summary>
internal static class ResourceHistoryCommands
{
    public const string SectionPrefix = "@@rh:";

    /// <summary>Process CPU'sunu iki /proc okuması arasındaki gerçek artıştan hesaplamak için örnekleme süresi (saniye).</summary>
    public const int SampleSeconds = 2;

    /// <summary>
    /// /proc/[pid]/stat iki kez okunur (utime+stime farkı = gerçek CPU), arada /proc/stat ile toplam CPU ölçülür; ps kullanıcı,
    /// bellek ve komut satırını verir. Yetki gerektirmez.
    /// </summary>
    public static readonly string Processes = Wrap($"""
        echo @@rh:clk
        getconf CLK_TCK 2>/dev/null
        echo @@rh:ncpu
        grep -c '^cpu[0-9]' /proc/stat 2>/dev/null
        echo @@rh:cpu0
        head -n1 /proc/stat 2>/dev/null
        echo @@rh:t0
        cat /proc/[0-9]*/stat 2>/dev/null
        sleep {SampleSeconds}
        echo @@rh:cpu1
        head -n1 /proc/stat 2>/dev/null
        echo @@rh:t1
        cat /proc/[0-9]*/stat 2>/dev/null
        echo @@rh:ps
        ps -eo pid=,user:32=,pcpu=,pmem=,rss=,args= 2>/dev/null
        echo @@rh:end
        """);

    /// <summary>Tüm container'ların durum/sağlık/yeniden başlama sayısı (inspect) ve çalışanların kullanımı (stats). Docker yetkisi ister.</summary>
    public static readonly string Docker = Wrap("""
        if ! command -v docker >/dev/null 2>&1; then echo @@rh:nodocker; echo @@rh:end; exit 0; fi
        ids=$(docker ps -aq 2>/dev/null) || { echo @@rh:nodocker; echo @@rh:end; exit 0; }
        echo @@rh:inspect
        if [ -n "$ids" ]; then
          docker inspect --format '{{.Name}}|{{.RestartCount}}|{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}' $ids 2>/dev/null
        fi
        echo @@rh:stats
        if [ -n "$ids" ]; then
          docker stats --no-stream --no-trunc --format '{{json .}}' 2>/dev/null
        fi
        echo @@rh:end
        """);

    private static string Wrap(string script) => "sh -c " + ShellQuote.Quote(script.Replace("\r\n", "\n"));
}
