using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plandex.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Plandex.Api.Tests;

public class PlandexAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("plandex_test")
        .WithUsername("plandex")
        .WithPassword("plandex123")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-chars",
                ["Jwt:Issuer"] = "plandex-test"
            });
        });

        builder.ConfigureServices(services =>
        {
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlandexDbContext>();
            db.Database.Migrate();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlandexDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE users, refresh_tokens, boards, lists, cards, labels, card_labels, checklists, checklist_items, time_entries RESTART IDENTITY CASCADE;");
    }
}
