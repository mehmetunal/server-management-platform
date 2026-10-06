using ServerManager.Application.Plugins;

namespace ServerManager.Application.Authorization;

public sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly IReadOnlyList<PermissionInfo> _all;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<IPermissionProvider> _providers;

    public PermissionCatalog(IEnumerable<IPermissionProvider> providers, IPluginCatalog? pluginCatalog = null)
    {
        _providers = providers.ToList();
        var list = new List<PermissionInfo>();

        foreach (var permission in Permissions.All)
        {
            if (_names.Add(permission))
                list.Add(new PermissionInfo(permission, Permissions.DisplayNames.GetValueOrDefault(permission, permission), PermissionGroups.For(permission), false));
        }

        foreach (var provider in _providers)
        {
            var group = pluginCatalog?.FindByAssembly(provider.GetType().Assembly)?.Descriptor.FriendlyName ?? "Eklenti";
            foreach (var definition in provider.GetPermissions())
            {
                if (_names.Add(definition.Name))
                    list.Add(new PermissionInfo(definition.Name, definition.DisplayName, group, true));
            }
        }

        _all = list;
    }

    public IReadOnlyList<PermissionInfo> All => _all;

    public bool IsKnown(string permission) => _names.Contains(permission);

    public IReadOnlyList<string> DefaultsFor(string roleName)
    {
        if (string.Equals(roleName, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return _all.Select(p => p.Name).ToList();

        var result = new List<string>();
        var role = Roles.All.FirstOrDefault(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        if (role is null)
            return result;

        if (DefaultRolePermissions.Matrix.TryGetValue(role, out var core))
            result.AddRange(core);

        foreach (var provider in _providers)
        {
            if (provider.GetDefaultRolePermissions().TryGetValue(role, out var pluginDefaults))
                result.AddRange(pluginDefaults);
        }

        return result.Distinct(StringComparer.Ordinal).ToList();
    }
}
