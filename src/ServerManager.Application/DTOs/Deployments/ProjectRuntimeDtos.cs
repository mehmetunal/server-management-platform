namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Projeye ait container (Compose servisi veya Dockerfile container'ı).</summary>
public sealed class ProjectContainerDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Compose'da container adından çıkarılan servis adı; Dockerfile'da null.</summary>
    public string? Service { get; init; }

    public string State { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;
}

public sealed class ProjectLogQuery
{
    public string Container { get; set; } = string.Empty;

    public int? Tail { get; set; }

    /// <summary>RFC3339 zaman damgası; canlı takipte yalnızca bu andan sonraki satırlar.</summary>
    public string? Since { get; set; }

    /// <summary>Yalnızca error / fatal / panic / exception / warn geçen satırlar.</summary>
    public bool ProblemsOnly { get; set; }
}

public sealed class ProjectLogLineDto
{
    public DateTime? Timestamp { get; init; }

    public string? RawTimestamp { get; init; }

    public string Text { get; init; } = string.Empty;

    /// <summary>stderr'den geldi.</summary>
    public bool IsError { get; init; }

    /// <summary>Hata/uyarı deseniyle eşleşiyor (bkz. <c>RuntimeLogFilter</c>).</summary>
    public bool IsProblem { get; init; }
}

public sealed class ProjectLogsDto
{
    public string Container { get; init; } = string.Empty;

    public IReadOnlyList<ProjectLogLineDto> Lines { get; init; } = [];

    public bool Truncated { get; init; }

    /// <summary>Filtre uygulanmadan önceki satır sayısı; canlı takip zaman damgası bu satırlara göre ilerler.</summary>
    public int TotalLines { get; init; }

    /// <summary>Filtre sonucu boş olsa da canlı takibin devam edeceği son zaman damgası.</summary>
    public string? LastRawTimestamp { get; init; }
}
