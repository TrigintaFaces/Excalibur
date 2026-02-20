using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.Postgres.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresAuditServiceCollectionExtensionsShould
{
	[Fact]
	public void Register_audit_store_services_with_action()
	{
		var services = new ServiceCollection();

		services.AddPostgresAuditStore(o =>
		{
			o.ConnectionString = "Host=localhost;Database=audit";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(PostgresAuditStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditStore));
	}

	[Fact]
	public void Throw_for_null_services()
	{
		Should.Throw<ArgumentNullException>(() =>
			PostgresAuditServiceCollectionExtensions.AddPostgresAuditStore(
				null!,
				_ => { }));
	}

	[Fact]
	public void Throw_for_null_configure_action()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddPostgresAuditStore((Action<PostgresAuditOptions>)null!));
	}
}
