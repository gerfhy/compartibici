using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CompartiBici.Data;
using CompartiBici.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Puerto para Render ($PORT)
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// 2. Base de Datos SQLite (Soporte local y disco persistente Render /var/data/app.db)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? "Data Source=app.db";

if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var rawDataSource = connectionString.Split("Data Source=")[1].Split(';')[0].Trim();
    var dbDirectory = Path.GetDirectoryName(rawDataSource);
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 3. Identity con Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 4. Redis Cloud (Caché distribuida 60s)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] 
    ?? builder.Configuration["Redis__ConnectionString"]
    ?? Environment.GetEnvironmentVariable("Redis__ConnectionString")
    ?? Environment.GetEnvironmentVariable("REDIS_URL");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.InstanceName = "CompartiBici_";
        if (redisConnectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) || 
            redisConnectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(redisConnectionString);
            var userInfo = uri.UserInfo.Split(':');
            var password = userInfo.Length > 1 ? userInfo[1] : userInfo[0];
            var config = new ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port } },
                Password = password,
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
                AbortOnConnectFail = false
            };
            options.ConfigurationOptions = config;
        }
        else
        {
            options.Configuration = redisConnectionString;
        }
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// 5. Inyección de Servicios Externos (Algolia Search y PieSocket WebSockets)
builder.Services.AddHttpClient<IAlgoliaSearchService, AlgoliaSearchService>();
builder.Services.AddHttpClient<IPieSocketService, PieSocketService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// 6. Migraciones y Seed Automático
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(services);
}

// 7. Configurar Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
