using DataMigration.Contracts;
using DataMigration.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using WebDts.Blazor.Hubs;
using WebDts.Blazor.Models;
using WebDts.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/webdts-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntDesign();

// Add services

// Add HttpClient
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add Controllers for API
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WebDts API", Version = "v1" });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// Register DataMigration Core Services
builder.Services.AddSingleton<IPluginManager, PluginManager>();
builder.Services.AddScoped<IMigrationService>(sp => new MigrationService(sp.GetRequiredService<ILogger<MigrationService>>(), sp));

// Register WebDts Services
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IPluginService, PluginService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Register State Services
builder.Services.AddSingleton<WebDts.Blazor.State.AppState>();
builder.Services.AddSingleton<WebDts.Blazor.State.TaskState>();
builder.Services.AddSingleton<WebDts.Blazor.State.PluginState>();

// Configure WebDts Settings
builder.Services.Configure<WebDtsSettings>(
    builder.Configuration.GetSection("WebDts"));

var app = builder.Build();

// Initialize PluginManager
using (var scope = app.Services.CreateScope())
{
    var pluginManager = scope.ServiceProvider.GetRequiredService<IPluginManager>();
    var webDtsSettings = builder.Configuration.GetSection("WebDts").Get<WebDtsSettings>();
    var pluginsPath = Path.Combine(builder.Environment.ContentRootPath, webDtsSettings!.PluginsDirectory);
    
    if (Directory.Exists(pluginsPath))
    {
        pluginManager.LoadPlugins(pluginsPath);
        Log.Information("Plugins loaded from: {PluginsPath}", pluginsPath);
    }
    else
    {
        Log.Warning("Plugins directory not found: {PluginsPath}", pluginsPath);
        Directory.CreateDirectory(pluginsPath);
    }

    // Ensure upload directory exists
    var uploadPath = Path.Combine(builder.Environment.ContentRootPath, webDtsSettings.UploadDirectory);
    if (!Directory.Exists(uploadPath))
    {
        Directory.CreateDirectory(uploadPath);
        Log.Information("Upload directory created: {UploadPath}", uploadPath);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebDts API V1");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapHub<MigrationHub>("/migrationhub");
app.MapFallbackToPage("/_Host");

app.Run();
