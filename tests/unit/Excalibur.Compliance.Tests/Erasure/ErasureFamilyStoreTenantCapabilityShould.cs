// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds every erasure-family store registration to the dep-gated tenant-aware seam, across both shipped
/// relational providers.
/// </summary>
/// <remarks>
/// <para>
/// Each of these contracts is tenant-owned, and each store requires an <see cref="ITenantContext"/> and
/// binds its term on the statements it builds. The multi-tenancy gate refuses any registered tenant-owned
/// store that attests no mechanism, so a store registered plainly is not merely unattested -- it is a
/// correct store that makes the host refuse to start, and the obvious workaround (waiving the requirement)
/// waives it for genuinely unscoped stores too.
/// </para>
/// <para>
/// Both providers are covered for each contract on purpose. A lock over one of two shipped providers does
/// not hold the guarantee: reverting the uncovered provider to a bare registration drops its marker with
/// nothing going red.
/// </para>
/// <para>
/// Safety and liveness are both asserted. "A marker is present" is satisfied by a registration that emits
/// the marker and no longer produces a usable store, or that produces a second, unwired instance alongside
/// the attested one -- so each arm also resolves the contract and the concrete type and requires them to be
/// one instance, the instance the marker describes.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ErasureFamilyStoreTenantCapabilityShould
{
	// Never contacted: no arm here opens a connection. Each only has to parse.
	private const string PostgresConnectionString = "Host=localhost;Database=compliance";
	private const string SqlServerConnectionString = "Server=localhost;Database=compliance;Integrated Security=true";

	/// <summary>
	/// Registers one provider, then asserts the marker is present and describes the instance the contract
	/// actually resolves to.
	/// </summary>
	/// <typeparam name="TContract">The tenant-owned store contract.</typeparam>
	/// <typeparam name="TStore">The provider store the registration is expected to produce.</typeparam>
	/// <param name="register">The provider registration under test.</param>
	private static void ShouldAttestAndResolve<TContract, TStore>(Action<IServiceCollection> register)
		where TContract : class
		where TStore : class, TContract
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The erasure and data-inventory stores pseudonymize subject ids, and the hasher fails closed on an
		// unset pepper. Set it here so a missing-secret validation failure cannot stand in for the
		// attestation these arms are about.
		_ = services.Configure<DataSubjectHashingOptions>(
			static o => o.Pepper = "test-pepper-0123456789abcdef0123456789ab");

		register(services);

		using var provider = services.BuildServiceProvider();

		// SAFETY: without the marker the multi-tenancy gate fails closed on a store that scopes correctly.
		provider.GetService<ITenantScopingCapability<TContract>>().ShouldNotBeNull(
			typeof(TStore).Name + " requires an ambient tenant context and binds its term on every statement "
			+ "it builds, so it must attest that mechanism through the seam that wires it. Registered "
			+ "plainly it attests nothing, and the gate then refuses a host for a store that is correct.");

		// LIVENESS: the attestation must not have cost the registration it describes...
		var byContract = provider.GetRequiredService<TContract>();
		_ = byContract.ShouldBeOfType<TStore>();

		// ...and it must describe the instance the application uses, not a second, unwired construction.
		byContract.ShouldBeSameAs(
			provider.GetRequiredService<TStore>(),
			"the marker is bound to the instance the seam built. If the contract forwards to a different "
			+ "one, the attestation describes an object nobody resolves.");
	}

	[Fact]
	public void AttestScopingForThePostgresErasureStore() =>
		ShouldAttestAndResolve<IErasureStore, PostgresErasureStore>(
			s => s.AddPostgresErasureStore(o => o.ConnectionString = PostgresConnectionString));

	[Fact]
	public void AttestScopingForTheSqlServerErasureStore() =>
		ShouldAttestAndResolve<IErasureStore, SqlServerErasureStore>(
			s => s.AddSqlServerErasureStore(o => o.ConnectionString = SqlServerConnectionString));

	[Fact]
	public void AttestScopingForThePostgresLegalHoldStore() =>
		ShouldAttestAndResolve<ILegalHoldStore, PostgresLegalHoldStore>(
			s => s.AddPostgresLegalHoldStore(o => o.ConnectionString = PostgresConnectionString));

	[Fact]
	public void AttestScopingForTheSqlServerLegalHoldStore() =>
		ShouldAttestAndResolve<ILegalHoldStore, SqlServerLegalHoldStore>(
			s => s.AddSqlServerLegalHoldStore(o => o.ConnectionString = SqlServerConnectionString));

	[Fact]
	public void AttestScopingForThePostgresDataInventoryStore() =>
		ShouldAttestAndResolve<IDataInventoryStore, PostgresDataInventoryStore>(
			s => s.AddPostgresDataInventoryStore(o => o.ConnectionString = PostgresConnectionString));

	[Fact]
	public void AttestScopingForTheSqlServerDataInventoryStore() =>
		ShouldAttestAndResolve<IDataInventoryStore, SqlServerDataInventoryStore>(
			s => s.AddSqlServerDataInventoryStore(o => o.ConnectionString = SqlServerConnectionString));

	/// <summary>
	/// LIVENESS for the requirement every attestation above rests on: the tenant context is a REQUIRED
	/// constructor parameter.
	/// </summary>
	/// <remarks>
	/// If it were optional, a store could be built having been handed nothing, silently widen to the
	/// untenanted partition, and still be registered through the seam -- the marker would then attest a
	/// scoping that is not happening, which is worse than the missing marker these arms exist to prevent.
	/// </remarks>
	[Fact]
	public void RefuseConstructionWithoutATenantContext()
	{
		var tenantOptions = MsOptions.Create(new TenantContextOptions());

		_ = Should.Throw<ArgumentNullException>(() => new PostgresErasureStore(
			MsOptions.Create(new PostgresErasureStoreOptions { ConnectionString = PostgresConnectionString }),
			A.Fake<IDataSubjectHasher>(),
			NullLogger<PostgresErasureStore>.Instance,
			null!,
			tenantOptions));

		_ = Should.Throw<ArgumentNullException>(() => new SqlServerErasureStore(
			MsOptions.Create(new SqlServerErasureStoreOptions { ConnectionString = SqlServerConnectionString }),
			A.Fake<IDataSubjectHasher>(),
			NullLogger<SqlServerErasureStore>.Instance,
			null!,
			tenantOptions));

		_ = Should.Throw<ArgumentNullException>(() => new PostgresLegalHoldStore(
			MsOptions.Create(new PostgresLegalHoldStoreOptions { ConnectionString = PostgresConnectionString }),
			NullLogger<PostgresLegalHoldStore>.Instance,
			null!,
			tenantOptions));

		_ = Should.Throw<ArgumentNullException>(() => new SqlServerLegalHoldStore(
			MsOptions.Create(new SqlServerLegalHoldStoreOptions { ConnectionString = SqlServerConnectionString }),
			NullLogger<SqlServerLegalHoldStore>.Instance,
			null!,
			tenantOptions));

		_ = Should.Throw<ArgumentNullException>(() => new PostgresDataInventoryStore(
			MsOptions.Create(new PostgresDataInventoryStoreOptions { ConnectionString = PostgresConnectionString }),
			A.Fake<IDataSubjectHasher>(),
			NullLogger<PostgresDataInventoryStore>.Instance,
			null!,
			tenantOptions));

		_ = Should.Throw<ArgumentNullException>(() => new SqlServerDataInventoryStore(
			MsOptions.Create(new SqlServerDataInventoryStoreOptions { ConnectionString = SqlServerConnectionString }),
			A.Fake<IDataSubjectHasher>(),
			NullLogger<SqlServerDataInventoryStore>.Instance,
			null!,
			tenantOptions));
	}
}
