using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderService.Data;

namespace OrderService.HealthChecks;

public class DatabaseHealthCheck(OrderDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Order database is reachable.")
                : HealthCheckResult.Unhealthy("Order database could not be reached.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Order database check failed.",
                exception);
        }
    }
}
