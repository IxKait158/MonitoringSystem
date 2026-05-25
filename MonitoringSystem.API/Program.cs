using Microsoft.ML;
using MonitoringSystem.BLL;
using MonitoringSystem.BLL.Hubs;
using MonitoringSystem.DAL;
using MonitoringSystem.Middlewares;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/monitoring-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MonitoringSystem.API", Version = "v1" });
});

services.AddDAL(builder.Configuration);
services.AddBLL(builder.Configuration);

services.AddSingleton<MLContext>();

services.AddSignalR();

services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

await app.ApplyMigrations();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseSerilogRequestLogging();

app.UseApiKeyAuth();

app.MapControllers();
app.MapHub<MetricsHub>("/hub/metrics");

app.Run();
