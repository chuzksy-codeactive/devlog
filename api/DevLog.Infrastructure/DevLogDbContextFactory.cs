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
            .Build();

        var connectionString = configuration.GetConnectionString("DevLogDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DevLogDb not found — run dotnet ef commands from DevLog.Api so its appsettings files are picked up.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        var optionsBuilder = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(dataSourceBuilder.Build(), npgsqlOptions => npgsqlOptions.UseVector())
            .UseSnakeCaseNamingConvention();

        return new DevLogDbContext(optionsBuilder.Options);
    }
}
