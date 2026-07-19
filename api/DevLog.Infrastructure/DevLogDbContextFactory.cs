using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevLog.Infrastructure;

// Used only by `dotnet ef` tooling (migrations add/database update), never
// by the running app — EF's design-time tooling prefers an
// IDesignTimeDbContextFactory<T> over trying to spin up the real app host.
// Deliberately builds against ConnectionStrings:DevLogDb (the owner role,
// `devlog`), not the DevLogDbRuntime connection ServiceCollectionExtensions
// wires up for real app traffic — DDL (CREATE TABLE, ALTER TABLE, CREATE
// EXTENSION) needs owner/superuser rights that `devlog_app` deliberately
// doesn't have as of Milestone 8's RLS force-on-owner fix (see
// sql/005_create_app_role.sql). Run `dotnet ef` commands from DevLog.Api
// as GETTING-STARTED.md already instructs, so this picks up that project's
// appsettings files.
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
