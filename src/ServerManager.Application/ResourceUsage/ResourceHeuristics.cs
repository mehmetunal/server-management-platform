using System.Globalization;
using ServerManager.Application.Common;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.ResourceUsage;

public enum FindingSeverity
{
    Critical = 1,
    Warning = 2,
    Info = 3,
    Ok = 4
}

/// <summary>Bulgunun yönlendirdiği yer: sayfadaki bölüm veya Temizlik sayfası.</summary>
public enum FindingAction
{
    None = 0,
    Cpu,
    Memory,
    Disk,
    Io,
    Processes,
    Containers,
    Cleanup
}

public sealed record ResourceFinding(FindingSeverity Severity, string Title, string Detail, FindingAction Action = FindingAction.None);

/// <summary>"Neden yavaş?" paneli: basit eşiklerle Türkçe açıklamalı bulgular üretir.</summary>
public static class ResourceHeuristics
{
    public const double LoadWarningPerCore = 1.0;
    public const double LoadCriticalPerCore = 2.0;
    public const double MemoryAvailableWarningPercent = 10;
    public const double MemoryAvailableCriticalPercent = 5;
    public const double SwapWarningPercent = 50;
    public const double SwapCriticalPercent = 80;
    public const long SwapMinimumKilobytes = 64 * 1024;
    public const double IoWaitWarningPercent = 10;
    public const double IoWaitCriticalPercent = 20;
    public const double StealWarningPercent = 10;
    public const int DiskWarningPercent = 90;
    public const int DiskCriticalPercent = 95;
    public const double ProcessCpuWarningPercent = 80;
    public const double ContainerCpuWarningPercent = 80;
    public const double ContainerMemoryWarningPercent = 90;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static IReadOnlyList<ResourceFinding> Evaluate(
        ResourceSnapshot snapshot,
        ProcessList? processes,
        StorageSnapshot? storage,
        IReadOnlyList<ContainerUsage>? containers)
    {
        var findings = new List<ResourceFinding>();
        Load(snapshot, findings);
        Memory(snapshot, findings);
        Cpu(snapshot, findings);
        Disk(storage, findings);
        Processes(processes, findings);
        Oom(snapshot, findings);
        Containers(containers, findings);

        if (findings.Count == 0)
        {
            findings.Add(new ResourceFinding(FindingSeverity.Ok, "Belirgin bir darboğaz görünmüyor",
                "CPU yükü, bellek, swap, disk G/Ç ve doluluk eşiklerin altında. Yavaşlık sürüyorsa uygulama loglarına ve ağ gecikmesine bakın."));
        }

        return findings.OrderBy(f => f.Severity).ToList();
    }

    private static void Load(ResourceSnapshot snapshot, List<ResourceFinding> findings)
    {
        if (snapshot.Load1 is not { } load || snapshot.CpuCores is not > 0)
            return;

        var cores = snapshot.CpuCores.Value;
        var perCore = load / cores;
        if (perCore <= LoadWarningPerCore)
            return;

        var ioHint = snapshot.Cpu is { IoWait: >= IoWaitWarningPercent }
            ? " G/Ç beklemesi de yüksek; yükün bir kısmı diski bekleyen process'lerden geliyor olabilir."
            : " En çok CPU kullanan process'lere bakın.";
        findings.Add(new ResourceFinding(
            perCore > LoadCriticalPerCore ? FindingSeverity.Critical : FindingSeverity.Warning,
            "Yük ortalaması çekirdek sayısını aşıyor",
            $"1 dakikalık yük {Number(load)}, çekirdek sayısı {cores.ToString(CultureInfo.InvariantCulture)}: işler CPU sırası bekliyor.{ioHint}",
            FindingAction.Cpu));
    }

    private static void Memory(ResourceSnapshot snapshot, List<ResourceFinding> findings)
    {
        if (snapshot.Memory is not { TotalKilobytes: > 0 } memory)
            return;

        if (memory.AvailablePercent < MemoryAvailableWarningPercent)
        {
            findings.Add(new ResourceFinding(
                memory.AvailablePercent < MemoryAvailableCriticalPercent ? FindingSeverity.Critical : FindingSeverity.Warning,
                "Kullanılabilir bellek çok az",
                $"Toplam {Kilobytes(memory.TotalKilobytes)} belleğin yalnızca {Kilobytes(memory.AvailableKilobytes)} kadarı (%{Number(memory.AvailablePercent)}) kullanılabilir. " +
                "Sistem önbelleği boşaltıp swap'a yazmaya başlar; en çok RAM kullanan process ve container'lara bakın.",
                FindingAction.Memory));
        }

        if (memory.SwapUsedPercent is { } swap && memory.SwapUsedKilobytes >= SwapMinimumKilobytes && swap >= SwapWarningPercent)
        {
            findings.Add(new ResourceFinding(
                swap >= SwapCriticalPercent ? FindingSeverity.Critical : FindingSeverity.Warning,
                "Swap kullanımı yüksek",
                $"Swap'ın %{Number(swap)} kadarı ({Kilobytes(memory.SwapUsedKilobytes)}) dolu. Diske taşınan bellek sayfaları geri okunurken uygulamalar belirgin şekilde yavaşlar.",
                FindingAction.Memory));
        }
    }

    private static void Cpu(ResourceSnapshot snapshot, List<ResourceFinding> findings)
    {
        if (snapshot.Cpu is not { } cpu)
            return;

        if (cpu.IoWait >= IoWaitWarningPercent)
        {
            findings.Add(new ResourceFinding(
                cpu.IoWait >= IoWaitCriticalPercent ? FindingSeverity.Critical : FindingSeverity.Warning,
                "Disk G/Ç beklemesi yüksek",
                $"CPU zamanının %{Number(cpu.IoWait)} kadarı diski beklemekle geçiyor. Disk yavaş veya çok yoğun okunuyor/yazılıyor; G/Ç yapan process'lere ve swap kullanımına bakın.",
                FindingAction.Io));
        }

        if (cpu.Steal >= StealWarningPercent)
        {
            findings.Add(new ResourceFinding(FindingSeverity.Warning, "Sanal makinede CPU çalınıyor (steal)",
                $"CPU zamanının %{Number(cpu.Steal)} kadarı sağlayıcının diğer sanal makinelerine gidiyor. Sunucu planını yükseltmek veya sağlayıcıyla görüşmek gerekebilir.",
                FindingAction.Cpu));
        }
    }

    private static void Disk(StorageSnapshot? storage, List<ResourceFinding> findings)
    {
        if (storage is null)
            return;

        foreach (var fs in storage.FileSystems)
        {
            if (fs.UsePercent >= DiskWarningPercent)
            {
                findings.Add(new ResourceFinding(
                    fs.UsePercent >= DiskCriticalPercent ? FindingSeverity.Critical : FindingSeverity.Warning,
                    $"{fs.MountPoint} neredeyse dolu",
                    $"%{fs.UsePercent.ToString(CultureInfo.InvariantCulture)} dolu, {Kilobytes(fs.AvailableKilobytes)} boş. Disk dolarsa veritabanları ve loglar yazamaz. " +
                    "Temizlik sayfasından kullanılmayan Docker kaynaklarını ve eski logları silebilir, en büyük klasörleri aşağıdan bulabilirsiniz.",
                    FindingAction.Cleanup));
            }

            if (fs.InodeUsePercent is >= DiskWarningPercent)
            {
                findings.Add(new ResourceFinding(
                    fs.InodeUsePercent >= DiskCriticalPercent ? FindingSeverity.Critical : FindingSeverity.Warning,
                    $"{fs.MountPoint} üzerinde inode tükeniyor",
                    $"Inode'ların %{fs.InodeUsePercent.Value.ToString(CultureInfo.InvariantCulture)} kadarı kullanılıyor; boş alan olsa bile yeni dosya oluşturulamaz. " +
                    "Genelde çok sayıda küçük dosya (oturum, önbellek, /tmp) sebep olur.",
                    FindingAction.Cleanup));
            }
        }
    }

    private static void Processes(ProcessList? processes, List<ResourceFinding> findings)
    {
        if (processes is not { HasCpuUsage: true })
            return;

        foreach (var process in processes.Processes.Where(p => p.CpuPercent >= ProcessCpuWarningPercent).Take(3))
        {
            findings.Add(new ResourceFinding(FindingSeverity.Warning, $"{process.Name} CPU'yu yoğun kullanıyor",
                $"PID {process.Pid.ToString(CultureInfo.InvariantCulture)} ({process.User}) bir çekirdeğin %{Number(process.CpuPercent!.Value)} kadarını kullanıyor " +
                "(ps değeri, process başladığından beri ortalamadır).",
                FindingAction.Processes));
        }
    }

    private static void Oom(ResourceSnapshot snapshot, List<ResourceFinding> findings)
    {
        if (snapshot.OomEvents.Count == 0)
            return;

        var window = snapshot.OomSource == "journal" ? "son 24 saatte" : "çekirdek kayıtlarında (dmesg)";
        findings.Add(new ResourceFinding(FindingSeverity.Critical, "Bellek yetersizliğinden process öldürüldü (OOM)",
            $"Çekirdek {window} {snapshot.OomEvents.Count.ToString(CultureInfo.InvariantCulture)} OOM kaydı bıraktı. Bellek yetmediği için bir uygulama zorla kapatılmış; " +
            "bellek limitlerini, sızıntı yapan process'leri veya RAM artırımını değerlendirin.",
            FindingAction.Memory));
    }

    private static void Containers(IReadOnlyList<ContainerUsage>? containers, List<ResourceFinding> findings)
    {
        if (containers is null)
            return;

        foreach (var container in containers.Where(c => c.Stats.CpuPercent >= ContainerCpuWarningPercent).Take(3))
        {
            findings.Add(new ResourceFinding(FindingSeverity.Warning, $"{container.Name} container'ı CPU'yu yoğun kullanıyor",
                $"Anlık CPU kullanımı %{Number(container.Stats.CpuPercent)} (100 = bir çekirdek).", FindingAction.Containers));
        }

        foreach (var container in containers.Where(c => c.Stats.MemoryPercent >= ContainerMemoryWarningPercent && c.Stats.MemoryLimitBytes > 0).Take(3))
        {
            findings.Add(new ResourceFinding(FindingSeverity.Warning, $"{container.Name} bellek limitine yaklaştı",
                $"Container belleğinin %{Number(container.Stats.MemoryPercent)} kadarını ({ByteSize.Format(container.Stats.MemoryUsageBytes)} / {ByteSize.Format(container.Stats.MemoryLimitBytes)}) kullanıyor; limit aşılırsa OOM ile yeniden başlar.",
                FindingAction.Containers));
        }
    }

    private static string Number(double value) => value.ToString("0.#", Turkish);

    private static string Kilobytes(long kilobytes) => ByteSize.Format(kilobytes * 1024);
}
