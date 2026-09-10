using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderService.HealthChecks;

public class InventoryServiceHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("inventory-health");
            using var response = await client.GetAsync("/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("InventoryService is reachable.")
                : HealthCheckResult.Unhealthy(
                    $"InventoryService health returned {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "InventoryService health check failed.",
                exception);
        }
    }
}
