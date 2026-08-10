using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevLog.Infrastructure;

public class DevLogDbContextFactory : IDesignTimeDbContextFactory<DevLogDbContext>
{
    public DevLogDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DevLogDb")
            ?? configuration.GetConnectionString("DevLogDbRuntime")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DevLogDb or ConnectionStrings:DevLogDbRuntime not found.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        var optionsBuilder = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(dataSourceBuilder.Build(), npgsqlOptions => npgsqlOptions.UseVector())
            .UseSnakeCaseNamingConvention();

        return new DevLogDbContext(optionsBuilder.Options);
    }
}
