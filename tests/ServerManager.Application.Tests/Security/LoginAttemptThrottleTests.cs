using Microsoft.Extensions.Caching.Memory;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Application.Tests.Security;

public class LoginAttemptThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_failures_do_not_require_waiting()
    {
        var throttle = new LoginAttemptThrottle(new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(Start));

        for (var i = 0; i < LoginAttemptThrottle.FreeAttempts - 1; i++)
            throttle.RegisterFailure("a@x.com", "10.0.0.1");

        Assert.Null(throttle.GetRetryAfter("a@x.com", "10.0.0.1"));
    }

    [Fact]
    public void Delay_grows_per_source_and_does_not_affect_other_ips()
    {
        var time = new FixedTimeProvider(Start);
        var throttle = new LoginAttemptThrottle(new MemoryCache(new MemoryCacheOptions()), time);

        for (var i = 0; i < LoginAttemptThrottle.FreeAttempts; i++)
            throttle.RegisterFailure("A@x.com ", "10.0.0.1");

        Assert.Equal(TimeSpan.FromSeconds(2), throttle.GetRetryAfter("a@x.com", "10.0.0.1"));
        Assert.Null(throttle.GetRetryAfter("a@x.com", "10.0.0.2"));
        Assert.Equal(LoginAttemptThrottle.FreeAttempts, throttle.GetAccountFailures("a@x.com"));

        throttle.RegisterFailure("a@x.com", "10.0.0.1");
        Assert.Equal(TimeSpan.FromSeconds(4), throttle.GetRetryAfter("a@x.com", "10.0.0.1"));
    }

    [Fact]
    public void Delay_is_capped()
    {
        Assert.Equal(LoginAttemptThrottle.MaxDelay, LoginAttemptThrottle.DelayFor(100));
    }

    [Fact]
    public void Reset_clears_counters()
    {
        var throttle = new LoginAttemptThrottle(new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(Start));
        for (var i = 0; i < 5; i++)
            throttle.RegisterFailure("a@x.com", "10.0.0.1");

        throttle.Reset("a@x.com", "10.0.0.1");

        Assert.Null(throttle.GetRetryAfter("a@x.com", "10.0.0.1"));
        Assert.Equal(0, throttle.GetAccountFailures("a@x.com"));
    }
}
