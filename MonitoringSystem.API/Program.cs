using Microsoft.ML;
using Microsoft.OpenApi;
using MonitoringSystem.BLL;
using MonitoringSystem.BLL.Hubs;
using MonitoringSystem.DAL;
using MonitoringSystem.Middlewares;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/monitoring-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MonitoringSystem.API",
        Version = "v1"
    });

    if (builder.Environment.IsDevelopment())
        options.OperationFilter<ApiKeyHeaderOperationFilter>();
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

public class ApiKeyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-API-KEY",
            In = ParameterLocation.Header,
            Required = false,
            Description = "API ключ для доступу до ендпоінтів",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}