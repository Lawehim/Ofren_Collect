using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Repository.Persistence;
using OfrenCollect.Repository.Persistence.Repositories;

namespace OfrenCollect.Repository;

/// <summary>Registers the persistence layer: the DbContext, repositories, and unit of work.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddRepository(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OfrenDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentEventRepository, PaymentEventRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IMandateRepository, MandateRepository>();
        services.AddScoped<IMandateDebitRepository, MandateDebitRepository>();
        services.AddScoped<IDueMandateDebitReader, DueMandateDebitReader>();
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IDashboardReader, DashboardReader>();
        services.AddScoped<ITransactionReader, TransactionReader>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<IAssistantData, AssistantDataReader>();
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return services;
    }
}
