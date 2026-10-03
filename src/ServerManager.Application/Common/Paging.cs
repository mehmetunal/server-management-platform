namespace ServerManager.Application.Common;

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
