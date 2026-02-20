using Excalibur.Dispatch.AuditLogging.Alerting;
using Excalibur.Dispatch.AuditLogging.Encryption;
using Excalibur.Dispatch.AuditLogging.Retention;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class AuditLoggingServiceCollectionExtensionsShould
{
    [Fact]
    public void Register_default_audit_store()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditLogging();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Register_default_audit_logger()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditLogging();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditLogger) &&
            d.ImplementationType == typeof(DefaultAuditLogger) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Register_in_memory_store_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditLogging();

        services.ShouldContain(d =>
            d.ServiceType == typeof(InMemoryAuditStore) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Register_custom_audit_store_via_generic()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditLogging<InMemoryAuditStore>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) &&
            d.ImplementationType == typeof(InMemoryAuditStore));
    }

    [Fact]
    public void Register_custom_audit_store_via_factory()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditLogging(_ => new InMemoryAuditStore());

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) &&
            d.ImplementationFactory != null);
    }

    [Fact]
    public void Replace_audit_store_with_use_audit_store()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditLogging();

        services.UseAuditStore<InMemoryAuditStore>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) &&
            d.ImplementationType == typeof(InMemoryAuditStore));
    }

    [Fact]
    public void Return_service_collection_from_add_audit_logging()
    {
        var services = new ServiceCollection();

        var result = services.AddAuditLogging();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void Return_service_collection_from_generic_add_audit_logging()
    {
        var services = new ServiceCollection();

        var result = services.AddAuditLogging<InMemoryAuditStore>();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void Return_service_collection_from_factory_add_audit_logging()
    {
        var services = new ServiceCollection();

        var result = services.AddAuditLogging(_ => new InMemoryAuditStore());

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void Return_service_collection_from_use_audit_store()
    {
        var services = new ServiceCollection();
        services.AddAuditLogging();

        var result = services.UseAuditStore<InMemoryAuditStore>();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_add_audit_logging()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAuditLogging());
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_generic_add_audit_logging()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAuditLogging<InMemoryAuditStore>());
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_factory_add_audit_logging()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAuditLogging(_ => new InMemoryAuditStore()));
    }

    [Fact]
    public void Throw_argument_null_for_null_factory()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddAuditLogging((Func<IServiceProvider, IAuditStore>)null!));
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_use_audit_store()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.UseAuditStore<InMemoryAuditStore>());
    }

    [Fact]
    public void Register_rbac_audit_store_as_decorator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditLogging();
        services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

        services.AddRbacAuditStore();

        // The RBAC decorator replaces the original IAuditStore registration
        // with a factory that wraps the inner store
        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) &&
            d.ImplementationFactory != null);
    }

    [Fact]
    public void Throw_when_rbac_added_without_base_store()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.AddRbacAuditStore());
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_rbac()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddRbacAuditStore());
    }

    [Fact]
    public void Register_audit_role_provider()
    {
        var services = new ServiceCollection();

        services.AddAuditRoleProvider<TestRoleProvider>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditRoleProvider) &&
            d.ImplementationType == typeof(TestRoleProvider) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_role_provider()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAuditRoleProvider<TestRoleProvider>());
    }

    [Fact]
    public void Register_audit_alerting()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAuditAlerting(opts => opts.MaxAlertsPerMinute = 50);

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditAlertService) &&
            d.ImplementationType == typeof(DefaultAuditAlertService));
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_alerting()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() =>
            services.AddAuditAlerting(opts => opts.MaxAlertsPerMinute = 50));
    }

    [Fact]
    public void Throw_argument_null_for_null_configure_in_alerting()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddAuditAlerting(null!));
    }

    [Fact]
    public void Register_audit_retention_service()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditLogging();

        services.AddAuditRetention(opts =>
        {
            opts.RetentionPeriod = TimeSpan.FromDays(365);
            opts.CleanupInterval = TimeSpan.FromHours(1);
        });

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditRetentionService) &&
            d.ImplementationType == typeof(DefaultAuditRetentionService));
    }

    [Fact]
    public void Register_retention_background_service()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditLogging();

        services.AddAuditRetention(opts => opts.CleanupInterval = TimeSpan.FromHours(1));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(AuditRetentionBackgroundService));
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_retention()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() =>
            services.AddAuditRetention(opts => opts.CleanupInterval = TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Throw_argument_null_for_null_configure_in_retention()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddAuditRetention(null!));
    }

    [Fact]
    public void Register_encryption_decorator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditLogging();
        services.AddSingleton(A.Fake<IEncryptionProvider>());

        services.UseAuditLogEncryption(opts => opts.EncryptReason = true);

        // After decoration, IAuditStore should be factory-registered
        services.ShouldContain(d =>
            d.ServiceType == typeof(IAuditStore) &&
            d.ImplementationFactory != null);
    }

    [Fact]
    public void Throw_argument_null_for_null_services_in_encryption()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.UseAuditLogEncryption());
    }

    [Fact]
    public void Throw_when_encryption_added_without_base_store()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.UseAuditLogEncryption());
    }

    private sealed class TestRoleProvider : IAuditRoleProvider
    {
        public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken)
            => Task.FromResult(AuditLogRole.Administrator);
    }
}
