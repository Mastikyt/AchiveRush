using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WebApplication1.Infrastructure;
using WebApplication1.infrastructure;
using WebApplication1.Services;


public class Program
{
    public static async Task Main(String[] args)
    {

        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = InputSizeLimits.MaxRequestBodyBytes;
            options.Limits.MaxRequestLineSize = InputSizeLimits.MaxQueryStringLength + 1024;
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        });

        builder.Services.Configure<FormOptions>(options =>
        {
            options.KeyLengthLimit = InputSizeLimits.MaxFieldNameLength;
            options.ValueLengthLimit = InputSizeLimits.MaxFieldValueLength;
            options.ValueCountLimit = InputSizeLimits.MaxFormValueCount;
            options.MultipartBodyLengthLimit = InputSizeLimits.MaxRequestBodyBytes;
            options.MultipartHeadersLengthLimit = 16 * 1024;
        });

        builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")));



        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 3;

            options.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(3);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });


        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddSteam(options =>
        {
            options.ApplicationKey = builder.Configuration["Steam:ApiKey"];
        });


        builder.Services.AddControllersWithViews();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient<SteamService>();
        builder.Services.AddScoped<SteamService>();
        builder.Services.AddScoped<SteamUserManager>();
        builder.Services.AddScoped<CacheService>();
        builder.Services.AddScoped<AdminAccessService>();
        builder.Services.AddScoped<QuestProgressService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<AchievementSyncService>();
        builder.Services.AddScoped<CustomAchievementVotingService>();
        builder.Services.AddHostedService<CustomAchievementVotingHostedService>();
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false")
        );
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSchemaGuard.EnsureAsync(db);
            await db.Database.MigrateAsync();
        }

        app.UseStatusCodePagesWithReExecute("/error/{0}");

        app.UseStaticFiles();

        app.UseMiddleware<InputSizeLimitMiddleware>();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSession();
        app.UseMiddleware<BanGuardMiddleware>();


        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        await app.RunAsync();

    }
}
