using InventoryService.Extensions;
using InventoryService.Startup;
using InventoryService.Data;
using InventoryService.Messaging;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using InventoryService.Services;
using Serilog;

// Create the application builder. It collects configuration and services
// before the actual web application is created.
var builder = WebApplication.CreateBuilder(args);

// Configure Serilog so application logs are written to the console.
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// Register HTTP controller support.
builder.Services.AddControllers();

// Register API metadata used by Swagger/OpenAPI.
builder.Services.AddEndpointsApiExplorer();

// Register Swagger document generation.
builder.Services.AddSwaggerGen();

// Register gRPC support.
builder.Services.AddGrpc();

// Read the Inventory database connection string from configuration.
// Stop startup if the required setting is missing.
var inventoryConnectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("Connection string 'Inventory' is required.");

// Register Entity Framework Core and configure it to use SQL Server.
// EnableRetryOnFailure allows temporary database failures to be retried.
builder.Services.AddDbContext<InventoryDbContext>(options =>
{
    options.UseSqlServer(
        inventoryConnectionString,
        sqlServer => sqlServer.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorNumbersToAdd: null));
});

// When code asks for IInventoryStore, dependency injection creates InventoryStore.
// Scoped means one instance is used within one request or service scope.
builder.Services.AddScoped<IInventoryStore, InventoryStore>();

// Register Redis as the distributed cache provider.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.InstanceName = "inventory:";
});

// Start the RabbitMQ consumer as a background service when the application starts.
builder.Services.AddHostedService<OrderCreatedConsumer>();

// Register support for the /health endpoint.
builder.Services.AddHealthChecks();

// Register OpenTelemetry tracing and write trace information to the console.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter();
    });

// Build the web application using all the registrations above.
var app = builder.Build();

// Apply database migrations and insert initial inventory data if needed.
await app.InitializeInventoryDatabaseAsync();

// Enable Swagger only in the Development environment.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Expose HTTP controller routes.
app.MapControllers();

// Expose the InventoryLookup gRPC service.
app.MapGrpcService<global::InventoryService.Grpc.InventoryLookupGrpcService>();

// Expose GET /health for service-health checks.
app.MapHealthChecks("/health");

// Start the web server and keep the application running.
app.Run();
