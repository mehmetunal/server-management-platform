using ServerManager.Application.Deployments;
using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.ServerSystem;

public enum PanelOwnerKind
{
    Project = 1,
    Service = 2,
    Proxy = 3,
    Panel = 4
}

/// <param name="Id">Panelde kayıtlı proje / servis kimliği; panelin bilmediği (silinmiş, başka panel) kaynaklarda null.</param>
public sealed record PanelOwner(PanelOwnerKind Kind, string? Slug, string? Name, Guid? Id = null)
{
    public string Label => Kind switch
    {
        PanelOwnerKind.Project => Name is not null ? $"Panel projesi: {Name}" : $"Panel projesi ({Slug})",
        PanelOwnerKind.Service => Name is not null ? $"Panel servisi: {Name}" : $"Panel servisi ({Slug})",
        PanelOwnerKind.Proxy => "Panel vekil sunucusu (Traefik)",
        _ => "Panel tarafından oluşturulmuş"
    };
}

/// <summary>Panelin bu sunucuda bildiği projeler ve servisler (kısa ad → kayıt).</summary>
public sealed record PanelResourceContext(
    IReadOnlyDictionary<string, PanelResourceRef> Projects,
    IReadOnlyDictionary<string, PanelResourceRef> Services)
{
    public static readonly PanelResourceContext Empty = new(
        new Dictionary<string, PanelResourceRef>(StringComparer.Ordinal),
        new Dictionary<string, PanelResourceRef>(StringComparer.Ordinal));
}

public sealed record PanelResourceRef(Guid Id, string Name);

/// <summary>
/// Bir Docker kaynağının panel projesine veya yönetilen servise ait olup olmadığını etiketlerden ve adlandırma kuralından
/// (<c>sm-&lt;proje&gt;</c>, <c>sm-svc-&lt;servis&gt;</c>, compose projesi <c>sm-&lt;proje&gt;</c>) çıkarır.
/// </summary>
public static class PanelOwnership
{
    public const string ProjectLabel = "sm.project";
    public const string ServiceLabel = "sm.service";
    public const string ManagedLabel = "sm.managed";
    public const string ComposeProjectLabel = "com.docker.compose.project";
    public const string ProxyContainer = "sm-traefik";
    private const string PanelPrefix = "sm-";

    public static PanelOwner? Resolve(string? name, IReadOnlyDictionary<string, string> labels, PanelResourceContext context)
    {
        if (labels.TryGetValue(ServiceLabel, out var service) && service.Length > 0)
            return Service(service, context);
        if (labels.TryGetValue(ProjectLabel, out var project) && project.Length > 0)
            return Project(project, context);

        if (labels.TryGetValue(ComposeProjectLabel, out var compose) && compose.StartsWith(PanelPrefix, StringComparison.Ordinal))
            return Project(compose[PanelPrefix.Length..], context);

        if (!string.IsNullOrEmpty(name))
        {
            if (name.StartsWith(ManagedServiceNames.Prefix, StringComparison.Ordinal))
                {
                var slug = name[ManagedServiceNames.Prefix.Length..];
                return Service(context.Services.ContainsKey(slug) ? slug : StripVolumeSuffix(slug), context);
            }
            if (name == ProxyContainer)
                return new PanelOwner(PanelOwnerKind.Proxy, null, null);
            if (name.StartsWith(PanelPrefix, StringComparison.Ordinal) && context.Projects.ContainsKey(name[PanelPrefix.Length..]))
                return Project(name[PanelPrefix.Length..], context);
        }

        return labels.TryGetValue(ManagedLabel, out var managed) && managed == "true"
            ? new PanelOwner(PanelOwnerKind.Panel, null, null)
            : null;
    }

    /// <summary>Deploy imajı <c>sm-&lt;slug&gt;:&lt;commit&gt;</c>; panelin bildiği bir projeye aitse proje döner.</summary>
    public static PanelOwner? ResolveImage(string repository, PanelResourceContext context)
    {
        if (!repository.StartsWith(PanelPrefix, StringComparison.Ordinal) || repository.StartsWith(ManagedServiceNames.Prefix, StringComparison.Ordinal))
            return null;

        var slug = repository[PanelPrefix.Length..];
        return DeploymentNames.IsValidSlug(slug) ? Project(slug, context) : null;
    }

    private static PanelOwner Project(string slug, PanelResourceContext context) =>
        context.Projects.TryGetValue(slug, out var project)
            ? new PanelOwner(PanelOwnerKind.Project, slug, project.Name, project.Id)
            : new PanelOwner(PanelOwnerKind.Project, slug, null);

    private static PanelOwner Service(string slug, PanelResourceContext context) =>
        context.Services.TryGetValue(slug, out var service)
            ? new PanelOwner(PanelOwnerKind.Service, slug, service.Name, service.Id)
            : new PanelOwner(PanelOwnerKind.Service, slug, null);

    private static string StripVolumeSuffix(string value) =>
        value.EndsWith("-data", StringComparison.Ordinal) ? value[..^"-data".Length] : value;
}
