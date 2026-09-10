using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Startup;

public static class DatabaseInitializer
{
    public static async Task InitializeInventoryDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // A failed seed attempt can leave entities tracked in the current DbContext.
                // Use a fresh scope and DbContext for every retry.
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

                await db.Database.MigrateAsync();

                if (!await db.InventoryItems.AnyAsync())
                {
                    db.InventoryItems.AddRange(
                        new InventoryItem { ProductId = 101, Name = "Laptop", Quantity = 10 },
                        new InventoryItem { ProductId = 102, Name = "Mouse", Quantity = 50 },
                        new InventoryItem { ProductId = 103, Name = "Keyboard", Quantity = 20 });
                    await db.SaveChangesAsync();
                }

                logger.LogInformation("SQL Server inventory database is ready.");
                return;
            }
            catch (Exception exception) when (attempt < 10)
            {
                logger.LogWarning(
                    exception,
                    "SQL Server is not ready. Retrying inventory database initialization ({Attempt}/10).",
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
