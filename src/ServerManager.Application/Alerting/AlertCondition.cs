namespace ServerManager.Application.Alerting;

/// <summary>Bir kuralın tek bir hedef (sunucu, kontrol, sertifika, proje) için o anki değerlendirmesi.</summary>
public sealed record AlertCondition(
    string TargetKey,
    string TargetName,
    Guid? ServerId,
    string? ServerName,
    AlertConditionState State,
    double? Value,
    string Message);
