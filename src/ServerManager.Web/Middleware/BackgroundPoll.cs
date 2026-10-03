namespace ServerManager.Web.Middleware;

public static class BackgroundPoll
{
    /// <summary>Sayfanın kendiliğinden yaptığı yoklama isteklerini işaretler (bkz. js/layout/alert-bell.js).</summary>
    public const string HeaderName = "X-Background-Poll";
}
