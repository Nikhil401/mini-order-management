using Microsoft.EntityFrameworkCore;
using OrderService.Data;

namespace OrderService.Startup;

public static class DatabaseInitializer
{
    public static async Task InitializeOrderDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await EnsureDatabaseAsync(db);
                startupLogger.LogInformation("SQL Server order database is ready.");
                return;
            }
            catch (Exception exception) when (attempt < 10)
            {
                startupLogger.LogWarning(
                    exception,
                    "SQL Server is not ready. Retrying database initialization ({Attempt}/10).",
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    private static async Task EnsureDatabaseAsync(OrderDbContext db)
    {
        try
        {
            await db.Database.MigrateAsync();
        }
        catch
        {
            if (await CanBaselineLegacySchemaAsync(db))
            {
                await BaselineLegacySchemaAsync(db);
                return;
            }

            throw;
        }
    }

    private static async Task<bool> CanBaselineLegacySchemaAsync(OrderDbContext db)
    {
        var connection = db.Database.GetDbConnection();

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END AS HasHistory,
                    CASE WHEN OBJECT_ID(N'dbo.Orders', N'U') IS NULL THEN 0 ELSE 1 END AS HasOrders
                """;

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return false;
            }

            var hasHistory = reader.GetInt32(0) == 1;
            var hasOrders = reader.GetInt32(1) == 1;
            return !hasHistory && hasOrders;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task BaselineLegacySchemaAsync(OrderDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (
                SELECT 1
                FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260828000000_InitialOrderSchema'
            )
            BEGIN
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260828000000_InitialOrderSchema', N'10.0.0');
            END
            """);
    }
}
