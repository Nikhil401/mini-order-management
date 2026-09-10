using OrderService.Extensions;
using OrderService.Startup;
using OrderService.Data;
using OrderService.HealthChecks;
using OrderService.Messaging;
using OrderService.Clients;
using OrderService.Services;
using InventoryService.Grpc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using System.Text;
using Serilog;

// Create the application builder object. It collects configuration and services, before the actual web application is created.
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

// Register HTTP controller support and configure validation error responses.
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest
        });
    };
});

// Register API documentation support.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Entity Framework Core with SQL Server.
var orderConnectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Connection string 'Orders' is required.");

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseSqlServer(
        orderConnectionString,
        sqlServer => sqlServer.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorNumbersToAdd: null));
});

// Register JWT authentication and authorization.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// Register the service that creates JWT access tokens during login.
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Register the InventoryService gRPC client and HTTP health client.
var grpcUri = GetRequiredServiceUri(builder.Configuration, "InventoryService:GrpcBaseUrl");
builder.Services.AddGrpcClient<InventoryLookup.InventoryLookupClient>(options =>
{
    options.Address = grpcUri;
});
builder.Services.AddTransient<IInventoryClient, InventoryClient>();

var inventoryUri = GetRequiredServiceUri(builder.Configuration, "InventoryService:BaseUrl");
builder.Services.AddHttpClient("inventory-health", client =>
{
    client.BaseAddress = inventoryUri;
    client.Timeout = TimeSpan.FromSeconds(3);
})
.AddPolicyHandler(PolicyExtensions.GetRetryPolicy())
.AddPolicyHandler(PolicyExtensions.GetCircuitBreakerPolicy())
.AddPolicyHandler(PolicyExtensions.GetTimeoutPolicy());

// Register the RabbitMQ publisher used when an order is created.
builder.Services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();

// Register health checks for the database and InventoryService.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<InventoryServiceHealthCheck>("inventory-service");

// Register OpenTelemetry tracing for HTTP requests and outgoing HTTP calls.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

// Build the web application using the registered services above.
var app = builder.Build();

// Apply database migrations and prepare the Orders database at startup.
await app.InitializeOrderDatabaseAsync();

// Enable Swagger only in the Development environment.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Send unexpected exceptions to the /error endpoint.
app.UseExceptionHandler("/error");

// Enable JWT authentication and authorization for protected endpoints.
app.UseAuthentication();
app.UseAuthorization();

// Return a safe ProblemDetails response for unexpected errors.
app.Map("/error", (HttpContext context) =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var problem = new ProblemDetails
    {
        Title = "An unexpected error occurred.",
        Status = StatusCodes.Status500InternalServerError,
        Detail = app.Environment.IsDevelopment()
            ? exception?.Message
            : "Please try again later."
    };

    return Results.Problem(
        problem.Detail,
        statusCode: problem.Status,
        title: problem.Title);
});

// Expose controller routes and the health-check endpoint.
app.MapControllers();
app.MapHealthChecks("/health");

// Start the web server and keep the application running.
app.Run();

static Uri GetRequiredServiceUri(IConfiguration configuration, string configurationKey)
{
    var url = configuration[configurationKey];

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            $"Configuration '{configurationKey}' must be a valid HTTP or HTTPS URL.");
    }

    return uri;
}
