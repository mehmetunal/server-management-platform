using Microsoft.Extensions.Caching.Memory;

namespace ServerManager.Infrastructure.Identity;

/// <summary>
/// Başarısız girişleri (hesap, IP) ve yalnızca hesap bazında bellekte sayar.
/// <para>
/// (hesap, IP) çifti için ilk <see cref="FreeAttempts"/> denemeden sonra her başarısız denemede bekleme süresi ikiye katlanır
/// (2 sn, 4 sn, ... en fazla <see cref="MaxDelay"/>). Bekleme dolmadan gelen denemede parola hiç kontrol edilmez ve
/// Identity kilit sayacı artmaz. Böylece tek bir kaynaktan kaba kuvvet yavaşlar; Identity kilidi
/// (<see cref="LockoutThreshold"/> deneme) yalnızca çok sayıda farklı IP'den gelen dağıtık saldırıya karşı son savunma olarak kalır
/// ve tek bir saldırganın bir hesabı birkaç denemeyle kilitlemesi zorlaşır.
/// </para>
/// <para>
/// Hesap bazındaki sayaç, olmayan e-postalar için de kilit mesajını aynı eşikte göstermeye yarar (kullanıcı tespiti engellenir).
/// Sayaçlar süreç içidir; birden fazla örnekte her örnek kendi sayacını tutar.
/// </para>
/// </summary>
public sealed class LoginAttemptThrottle
{
    public const int FreeAttempts = 3;
    public const int LockoutThreshold = 10;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public LoginAttemptThrottle(IMemoryCache cache, TimeProvider? timeProvider = null)
    {
        _cache = cache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <returns>Bu kaynaktan yeni deneme yapılabilmesi için kalan süre; beklemeye gerek yoksa null.</returns>
    public TimeSpan? GetRetryAfter(string email, string? ipAddress)
    {
        if (!_cache.TryGetValue(SourceKey(email, ipAddress), out Counter? counter) || counter is null)
            return null;

        lock (counter)
        {
            if (counter.Failures < FreeAttempts)
                return null;

            var remaining = counter.LastFailureAt + DelayFor(counter.Failures) - _timeProvider.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>Olmayan hesaplar için Identity kilidini taklit etmek üzere hesap bazındaki başarısız deneme sayısı.</summary>
    public int GetAccountFailures(string email) =>
        _cache.TryGetValue(AccountKey(email), out Counter? counter) && counter is not null ? counter.Read() : 0;

    public void RegisterFailure(string email, string? ipAddress)
    {
        Increment(SourceKey(email, ipAddress));
        Increment(AccountKey(email));
    }

    public void Reset(string email, string? ipAddress)
    {
        _cache.Remove(SourceKey(email, ipAddress));
        _cache.Remove(AccountKey(email));
    }

    internal static TimeSpan DelayFor(int failures)
    {
        if (failures < FreeAttempts)
            return TimeSpan.Zero;

        var exponent = Math.Min(failures - FreeAttempts + 1, 16);
        var delay = TimeSpan.FromSeconds(Math.Pow(2, exponent));
        return delay < MaxDelay ? delay : MaxDelay;
    }

    private void Increment(string key)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = Window;
            return new Counter();
        })!;

        lock (counter)
        {
            counter.Failures++;
            counter.LastFailureAt = _timeProvider.GetUtcNow();
        }
    }

    private static string Normalize(string email) => email.Trim().ToUpperInvariant();

    private static string SourceKey(string email, string? ipAddress) => $"login-fail:{Normalize(email)}|{ipAddress ?? "-"}";

    private static string AccountKey(string email) => $"login-fail:{Normalize(email)}";

    private sealed class Counter
    {
        public int Failures;
        public DateTimeOffset LastFailureAt;

        public int Read()
        {
            lock (this)
                return Failures;
        }
    }
}
