namespace ServerManager.Web.RateLimiting;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string ConnectionTest = "connection-test";
    public const string MetricsCollect = "metrics-collect";
}
