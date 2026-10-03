namespace ServerManager.Plugin.Git.GitHub.Tests.Fakes;

public sealed record RecordedRequest(string Method, string PathAndQuery, string? Authorization, string? ApiVersion, string UserAgent, string? Body);
