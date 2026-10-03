namespace ServerManager.Web.Framework.Navigation;

/// <summary>
/// Eklentinin sol menüye eklediği bağlantı. <see cref="Group"/> <see cref="MenuGroups"/> sabitlerinden biridir;
/// <see cref="Icon"/> <c>Icons.Render</c> adlarından biri olmalıdır.
/// </summary>
public sealed record MenuItem(
    string Group,
    string Title,
    string Tooltip,
    string Icon,
    string Permission,
    string Controller,
    string Action = "Index",
    int Order = 100);
