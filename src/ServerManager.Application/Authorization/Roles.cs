namespace ServerManager.Application.Authorization;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Developer = "Developer";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Admin, Operator, Developer, Viewer];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [SuperAdmin] = "Her şeye erişebilir.",
        [Admin] = "Sunucu ve deployment yönetebilir.",
        [Operator] = "Docker, log, servis ve terminal kullanabilir.",
        [Developer] = "Deployment, log ve sınırlı terminal kullanabilir.",
        [Viewer] = "Sadece görüntüleme."
    };
}
