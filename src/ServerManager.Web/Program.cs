using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using ServerManager.Application;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Monitoring;
using ServerManager.Infrastructure;
using ServerManager.Web.Authorization;
using ServerManager.Web.BackgroundJobs;
using ServerManager.Web.Extensions;
using ServerManager.Web.Hubs;
using ServerManager.Web.Middleware;
using ServerManager.Web.Models;
using ServerManager.Web.Options;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;

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

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IMonitoringNotifier, SignalRMonitoringNotifier>();
    if (builder.Configuration.GetValue($"{MonitoringOptions.SectionName}:Enabled", true))
    {
        builder.Services.AddHostedService<MetricsCollectorWorker>();
        builder.Services.AddHostedService<MetricsMaintenanceWorker>();
    }
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

    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        TurkishModelBindingMessages.Apply(options.ModelBindingMessageProvider);
    });

    builder.Services.AddAppRateLimiting();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync(app.Configuration);

    if (app.Configuration.GetValue("Proxy:TrustForwardedHeaders", false))
        app.UseForwardedHeaders();

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

    app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");
    app.MapHub<MonitoringHub>(MonitoringHub.Path);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Server Manager başlatılamadı");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
