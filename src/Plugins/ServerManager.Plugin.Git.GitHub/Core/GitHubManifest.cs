using System.Text.Json;
using System.Text.Json.Nodes;
using ServerManager.Application;

namespace ServerManager.Plugin.Git.GitHub.Core;

/// <summary>
/// GitHub App manifest'i. Yalnızca depo içeriği ve meta veri okuma izni istenir; webhook eklenmez
/// (panel çoğu zaman GitHub'dan erişilebilir değildir).
/// </summary>
public static class GitHubManifest
{
    public const string CallbackPath = "/GitHub/Callback";
    public const string SetupPath = "/GitHub/Setup";

    public static string Build(string name, string panelBaseUrl)
    {
        var root = panelBaseUrl.TrimEnd('/');
        var manifest = new JsonObject
        {
            ["name"] = name.Trim(),
            ["url"] = root,
            ["description"] = $"{ProductInfo.Name} deployment bağlantısı",
            ["redirect_url"] = root + CallbackPath,
            ["setup_url"] = root + SetupPath,
            ["setup_on_update"] = true,
            ["public"] = false,
            ["default_permissions"] = new JsonObject
            {
                ["contents"] = "read",
                ["metadata"] = "read"
            }
        };

        return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
