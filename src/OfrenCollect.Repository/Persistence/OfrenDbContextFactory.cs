using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations). It never connects — the
/// connection string is a non-secret placeholder so the tools can build the model. At
/// runtime the context is created by DI with the real connection string and tenant context.
/// </summary>
public sealed class OfrenDbContextFactory : IDesignTimeDbContextFactory<OfrenDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=ofren_collect;Username=ofren;Password=design-time-only";

    public OfrenDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OfrenDbContext>()
            .UseNpgsql(DesignTimeConnectionString)
            .Options;

        return new OfrenDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? CurrentTenantId => null;
    }
}
