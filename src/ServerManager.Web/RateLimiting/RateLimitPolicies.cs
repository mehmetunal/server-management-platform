namespace ServerManager.Web.RateLimiting;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string ConnectionTest = "connection-test";
    public const string MetricsCollect = "metrics-collect";
    public const string DockerAction = "docker-action";
    public const string FileAction = "file-action";
    public const string DeploymentAction = "deployment-action";
    public const string DeploymentLookup = "deployment-lookup";
    public const string AlertingAction = "alerting-action";
}
