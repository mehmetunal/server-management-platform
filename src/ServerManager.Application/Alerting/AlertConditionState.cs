namespace ServerManager.Application.Alerting;

public enum AlertConditionState
{
    /// <summary>Veri yok veya bayat; açık alarm olduğu gibi kalır, yeni alarm açılmaz.</summary>
    Unknown = 0,
    Ok = 1,
    Firing = 2
}
