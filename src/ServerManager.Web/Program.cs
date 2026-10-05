using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using ServerManager.Application;
using ServerManager.Application.Backups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Infrastructure;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.BackgroundJobs;
using ServerManager.Web.Backups;
using ServerManager.Web.Commands;
using ServerManager.Web.Deployments;
using ServerManager.Web.Extensions;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Plugins;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Hubs;
using ServerManager.Web.ManagedServices;
using ServerManager.Web.Middleware;
using ServerManager.Web.Models;
using ServerManager.Web.Options;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;
using ServerManager.Web.Terminal;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.Configure<TwoFactorOptions>(builder.Configuration.GetSection(TwoFactorOptions.SectionName));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddScoped<ServerPageBuilder>();
    builder.Services.AddSingleton<ScriptImportMap>();

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IMonitoringNotifier, SignalRMonitoringNotifier>();
    builder.Services.AddSingleton<TerminalAccessValidator>();
    builder.Services.AddSingleton<TerminalManager>();
    builder.Services.AddSingleton<IUserSessionRevoker, TerminalSessionRevoker>();
    builder.Services.AddHostedService<TerminalCommandWriter>();
    builder.Services.AddHostedService<TerminalIdleSweeper>();
    builder.Services.AddSingleton<DeploymentManager>();
    builder.Services.AddHostedService<DeploymentLifecycleWorker>();
    // Servisler (tek tıkla Docker servisleri)
    builder.Services.AddSingleton<ManagedServiceManager>();
    builder.Services.AddHostedService<ManagedServiceLifecycleWorker>();
    builder.Services.PostConfigure<BackupOptions>(options =>
        options.LocalRootPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.LocalRootPath)));
    builder.Services.AddSingleton<BackupManager>();
    builder.Services.AddHostedService<BackupLifecycleWorker>();
    builder.Services.AddSingleton<CommandRunManager>();
    builder.Services.AddHostedService<CommandRunLifecycleWorker>();
    builder.Services.AddHostedService<SecurityScanWorker>();
    builder.Services.AddHostedService<CloudSyncWorker>();
    builder.Services.AddHostedService<BackupSchedulerWorker>();
    builder.Services.AddHostedService<MetricsCollectorWorker>();
    builder.Services.AddHostedService<MetricsMaintenanceWorker>();
    builder.Services.AddHostedService<AlertEvaluationWorker>();
    builder.Services.AddHostedService<UptimeCheckWorker>();
    builder.Services.AddHostedService<SslCheckWorker>();
    builder.Services.AddHostedService<AlertingMaintenanceWorker>();
    builder.Services.Configure<WebEncoderOptions>(options =>
        options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    });

    var authCookieOptions = builder.Configuration.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>() ?? new AuthCookieOptions();
    var cookieSecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ServerManager.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(authCookieOptions.SessionTimeoutMinutes);
        options.SlidingExpiration = true;

        // Arka plan yoklamaları (alarm zili) oturum zaman aşımını uzatmaz; kullanıcı işlem yapmazsa oturum yine düşer.
        options.Events.OnCheckSlidingExpiration = context =>
        {
            if (context.Request.Headers.ContainsKey(BackgroundPoll.HeaderName))
                context.ShouldRenew = false;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToLogin = context =>
        {
            if (!context.Request.WantsJson())
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Oturum süresi doldu. Lütfen tekrar giriş yapın.", StatusCodes.Status401Unauthorized));
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (!context.Request.WantsJson())
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Bu işlem için yetkiniz yok.", StatusCodes.Status403Forbidden));
        };
    });

    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.FromMinutes(1));

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "RequestVerificationToken";
        options.Cookie.Name = "ServerManager.Antiforgery";
        options.Cookie.SecurePolicy = cookieSecurePolicy;
    });

    var mvcBuilder = builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        TurkishModelBindingMessages.Apply(options.ModelBindingMessageProvider);
    });

    builder.Services.AddAppRateLimiting();
    builder.AddPlugins(mvcBuilder);

    // Yalnızca listelenen proxy'lerden gelen X-Forwarded-* başlıklarına güvenilir. Liste boşsa varsayılan (loopback) kalır;
    // aksi halde istemci başlığı kendisi yazarak IP'sini (rate limit, audit) ve şemayı taklit edebilir.
    var knownProxies = ParseKnownProxies(builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>());
    var knownNetworks = ParseKnownNetworks(builder.Configuration.GetSection("Proxy:KnownNetworks").Get<string[]>());
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var proxy in knownProxies)
            options.KnownProxies.Add(proxy);
        foreach (var network in knownNetworks)
            options.KnownIPNetworks.Add(network);
    });

    // /health: süreç ayakta mı (veritabanına gitmez). /health/ready: veritabanına bağlanılabiliyor mu.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync(app.Configuration);

    if (app.Configuration.GetValue("Proxy:TrustForwardedHeaders", false))
    {
        if (knownProxies.Count == 0 && knownNetworks.Count == 0)
        {
            app.Logger.LogWarning(
                "Proxy:TrustForwardedHeaders açık fakat Proxy:KnownProxies / Proxy:KnownNetworks boş. Yalnızca loopback (127.0.0.1, ::1) üzerinden gelen başlıklara güvenilecek; ters proxy başka bir adresteyse ekleyin.");
        }

        app.UseForwardedHeaders();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TwoFactorEnforcementMiddleware>();

    app.MapHealthChecks(HealthEndpoints.LivenessPath, new HealthCheckOptions { Predicate = _ => false })
        .AllowAnonymous()
        .DisableRateLimiting();
    app.MapHealthChecks(HealthEndpoints.ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
        .AllowAnonymous()
        .DisableRateLimiting();

    app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    // Cookie süresi dolunca (veya oturum kapanınca) açık SignalR bağlantıları da kapanır; aksi halde bağlantı süresiz yaşar.
    app.MapHub<MonitoringHub>(MonitoringHub.Path, options => options.CloseOnAuthenticationExpiration = true);
    app.MapHub<TerminalHub>(TerminalHub.Path, options => options.CloseOnAuthenticationExpiration = true);
    app.MapHub<DeploymentHub>(DeploymentHub.Path, options => options.CloseOnAuthenticationExpiration = true);
    app.MapHub<ServiceHub>(ServiceHub.Path, options => options.CloseOnAuthenticationExpiration = true);
    app.MapPluginEndpoints();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "{Product} başlatılamadı", ProductInfo.Name);
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static List<IPAddress> ParseKnownProxies(string[]? values) =>
    (values ?? [])
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(v => IPAddress.TryParse(v.Trim(), out var address)
            ? address
            : throw new InvalidOperationException($"Proxy:KnownProxies içinde geçersiz IP adresi: {v}"))
        .ToList();

static List<System.Net.IPNetwork> ParseKnownNetworks(string[]? values) =>
    (values ?? [])
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(v => System.Net.IPNetwork.TryParse(v.Trim(), out var network)
            ? network
            : throw new InvalidOperationException($"Proxy:KnownNetworks içinde geçersiz CIDR: {v}"))
        .ToList();
