using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Authorization;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Models;

namespace ServerManager.Web.Helpers;

/// <summary>
/// Üst çubuktaki sayfa yolu. Görünüm <c>ViewData["Breadcrumb"]</c> vermediyse menüdeki bölüme göre kurulur.
/// Dosya düzenleme ve container ayrıntısında başlık "yaprak · sunucu" sırasındadır; diğer sunucu sayfalarında "sunucu · sayfa".
/// </summary>
public static class BreadcrumbTrail
{
    private sealed record Section(string Label, string Controller, string Action, string LinkTitle, string? Permission);

    private static readonly Dictionary<string, Section> Sections = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Servers"] = new("Sunucular", "Servers", "Index", "Sunucu listesine dön", Permissions.ServerView),
        ["ServerGroups"] = new("Gruplar", "ServerGroups", "Index", "Grup listesine dön", Permissions.ServerView),
        ["CloudAccounts"] = new("Sağlayıcılar", "CloudAccounts", "Index", "Bulut hesaplarına dön", Permissions.CloudView),
        ["Costs"] = new("Maliyet", "Costs", "Index", "Maliyet özetine dön", Permissions.ServerView),
        ["Projects"] = new("Projeler", "Projects", "Index", "Proje listesine dön", Permissions.DeploymentView),
        ["Deployments"] = new("Deployment'lar", "Deployments", "Index", "Deployment geçmişine dön", Permissions.DeploymentView),
        ["GitHub"] = new("GitHub", "GitHub", "Index", "GitHub sayfasına dön", null),
        ["CommandRuns"] = new("Toplu komut", "CommandRuns", "Index", "Toplu komutlara dön", Permissions.CommandView),
        ["ServerTemplates"] = new("Toplu komut", "CommandRuns", "Index", "Toplu komutlara dön", Permissions.CommandView),
        ["Uptime"] = new("Uptime", "Uptime", "Index", "Uptime kontrollerine dön", Permissions.AlertView),
        ["Alerts"] = new("Alarmlar", "Alerts", "Index", "Açık alarmlara dön", Permissions.AlertView),
        ["AlertRules"] = new("Alarmlar", "Alerts", "Index", "Açık alarmlara dön", Permissions.AlertView),
        ["NotificationChannels"] = new("Alarmlar", "Alerts", "Index", "Açık alarmlara dön", Permissions.AlertView),
        ["SslCertificates"] = new("SSL", "SslCertificates", "Index", "SSL sertifikalarına dön", Permissions.AlertView),
        ["BackupJobs"] = new("Yedekleme", "BackupJobs", "Index", "Yedek işlerine dön", Permissions.BackupView),
        ["BackupRuns"] = new("Yedekleme", "BackupJobs", "Index", "Yedek işlerine dön", Permissions.BackupView),
        ["BackupStorages"] = new("Yedekleme", "BackupJobs", "Index", "Yedek işlerine dön", Permissions.BackupView),
        ["Security"] = new("Güvenlik", "Security", "Index", "Güvenlik taramalarına dön", Permissions.SecurityView),
        ["Users"] = new("Kullanıcılar", "Users", "Index", "Kullanıcı listesine dön", Permissions.UserManage),
        ["Roles"] = new("Roller", "Roles", "Index", "Rol listesine dön", Permissions.RolesManage),
        ["ApiKeys"] = new("API anahtarları", "ApiKeys", "Index", "API anahtarlarına dön", null),
        ["AuditLogs"] = new("Audit Log", "AuditLogs", "Index", "Kayıt listesine dön", Permissions.AuditView),
        ["Plugins"] = new("Eklentiler", "Plugins", "Index", "Eklenti listesine dön", Permissions.PluginManage),
        ["Settings"] = new("Ayarlar", "Settings", "Index", "Ayarlara dön", Permissions.SettingsView),
        ["Account"] = new("Hesabım", "Account", "Security", "Hesap ayarlarına dön", null)
    };

    private static readonly HashSet<string> ServerControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Docker", "Terminal", "Files", "ServerSystem", "ServerSecurity", "ServerBackups",
        "ServerAlerts", "ServerActivity", "ServerDeployments", "ServerResources", "ServerCleanup", "Dokku", "Dokploy"
    };

    public static IReadOnlyList<BreadcrumbItem> Build(ViewContext viewContext, IUrlHelper url)
    {
        if (viewContext.ViewData["Breadcrumb"] is IReadOnlyList<BreadcrumbItem> custom && custom.Count > 0)
            return custom;

        var controller = viewContext.RouteData.Values["controller"] as string ?? string.Empty;
        var action = viewContext.RouteData.Values["action"] as string ?? string.Empty;
        var title = viewContext.ViewData["Title"] as string;
        var user = viewContext.HttpContext.User;
        var items = new List<BreadcrumbItem>();

        if (controller.Equals("Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new BreadcrumbItem("Dashboard"));
            return items;
        }

        if (user.HasPermission(Permissions.DashboardView))
            items.Add(new BreadcrumbItem("Dashboard", url.Action("Index", "Dashboard"), "Panele dön"));

        if (TryAppendServer(items, controller, action, title, viewContext, url, user))
            return items;

        Sections.TryGetValue(controller, out var section);
        var onSection = section is not null
            && controller.Equals(section.Controller, StringComparison.OrdinalIgnoreCase)
            && action.Equals(section.Action, StringComparison.OrdinalIgnoreCase);

        if (section is not null && !onSection)
            items.Add(Link(section, url, user));

        var current = string.IsNullOrWhiteSpace(title) ? section?.Label ?? controller : title;
        if (items.Count == 0 || !string.Equals(items[^1].Label, current, StringComparison.Ordinal))
            items.Add(new BreadcrumbItem(current));

        return items;
    }

    private static bool TryAppendServer(
        List<BreadcrumbItem> items,
        string controller,
        string action,
        string? title,
        ViewContext viewContext,
        IUrlHelper url,
        ClaimsPrincipal user)
    {
        var serverPage = ServerControllers.Contains(controller)
            || (controller.Equals("Servers", StringComparison.OrdinalIgnoreCase)
                && action is "Details" or "Edit" or "Metrics");
        if (!serverPage || string.IsNullOrWhiteSpace(title))
            return false;

        var id = viewContext.RouteData.Values["id"]?.ToString();
        if (!Guid.TryParse(id, out _))
            return false;

        var (serverName, page) = SplitServerTitle(controller, action, title);
        var servers = Sections["Servers"];
        items.Add(Link(servers, url, user));

        var serverUrl = user.HasPermission(Permissions.ServerView)
            ? url.Action("Details", "Servers", new { id })
            : null;
        if (string.IsNullOrWhiteSpace(page))
        {
            items.Add(new BreadcrumbItem(serverName));
            return true;
        }

        items.Add(new BreadcrumbItem(serverName, serverUrl, "Sunucu sayfasına dön"));

        if (controller.Equals("Files", StringComparison.OrdinalIgnoreCase) && action.Equals("Edit", StringComparison.OrdinalIgnoreCase))
        {
            var filesUrl = user.HasPermission(Permissions.FileView) ? url.Action("Index", "Files", new { id }) : null;
            items.Add(new BreadcrumbItem("Dosyalar", filesUrl, "Dosya listesine dön"));
        }
        else if (controller.Equals("Docker", StringComparison.OrdinalIgnoreCase) && action.Equals("Container", StringComparison.OrdinalIgnoreCase))
        {
            var listUrl = user.HasPermission(Permissions.DockerView) ? url.Action("Containers", "Docker", new { id }) : null;
            items.Add(new BreadcrumbItem("Container'lar", listUrl, "Container listesine dön"));
        }

        items.Add(new BreadcrumbItem(page));
        return true;
    }

    private static (string Server, string? Page) SplitServerTitle(string controller, string action, string title)
    {
        var parts = title.Split(" · ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return (title, null);

        var leafFirst = (controller.Equals("Files", StringComparison.OrdinalIgnoreCase) && action.Equals("Edit", StringComparison.OrdinalIgnoreCase))
            || (controller.Equals("Docker", StringComparison.OrdinalIgnoreCase) && action.Equals("Container", StringComparison.OrdinalIgnoreCase));
        return leafFirst ? (parts[1], parts[0]) : (parts[0], parts[1]);
    }

    private static BreadcrumbItem Link(Section section, IUrlHelper url, ClaimsPrincipal user)
    {
        var href = section.Permission is null || user.HasPermission(section.Permission)
            ? url.Action(section.Action, section.Controller)
            : null;
        return new BreadcrumbItem(section.Label, href, section.LinkTitle);
    }
}
