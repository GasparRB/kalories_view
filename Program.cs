using Kalories.Server.Data;
using Kalories.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Base de datos
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity: usuarios, registro y login
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// IA para analizar platos: el proveedor se elige en la configuración (Ai:Provider)
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

var provider = builder.Configuration["Ai:Provider"] ?? "Gemini";
if (provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IMealAnalyzer, ClaudeMealAnalyzer>(client =>
    {
        client.BaseAddress = new Uri("https://api.anthropic.com/");
        client.Timeout = TimeSpan.FromSeconds(90);
    });
}
else
{
    builder.Services.AddHttpClient<IMealAnalyzer, GeminiMealAnalyzer>(client =>
    {
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(90);
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<ApplicationUser>();
app.MapControllers();

app.Run();