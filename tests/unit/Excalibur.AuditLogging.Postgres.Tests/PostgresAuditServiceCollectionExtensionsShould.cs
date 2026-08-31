using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Postgres.Tests;

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

	/// <summary>
	/// The store attests the tenancy mechanism it actually implements, as part of the same registration act.
	/// </summary>
	/// <remarks>
	/// IAuditStore carries [TenantOwned] and is gated by AddMultiTenancy, so a host wiring this store beside
	/// the row discriminator is refused at startup unless the marker is present. The marker is obtainable
	/// only from the dep-gated seam -- its single member is internal to Excalibur.Dispatch.Abstractions, so no
	/// provider can register a look-alike -- which makes this arm the thing that goes red if the registration
	/// regresses to a bare TryAddSingleton. Scoping rather than partitioned is the correct attestation: every
	/// query this store builds binds the ambient tenant term as a scope, never as a caller-supplied filter.
	/// </remarks>
	[Fact]
	public void Attest_ambient_tenant_scoping_for_the_audit_contract()
	{
		var services = new ServiceCollection();

		services.AddPostgresAuditStore(o =>
		{
			o.ConnectionString = "Host=localhost;Database=audit";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(ITenantScopingCapability<IAuditStore>));
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
