// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Tests.Shared.Fixtures;

using PgStore = Excalibur.Compliance.Postgres.Erasure.PostgresErasureStore;
using PgStoreOptions = Excalibur.Compliance.Postgres.Erasure.PostgresErasureStoreOptions;
using SqlStore = Excalibur.Compliance.SqlServer.Erasure.SqlServerErasureStore;
using SqlStoreOptions = Excalibur.Compliance.SqlServer.Erasure.SqlServerErasureStoreOptions;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>
/// Shared inputs and the assertion that carries these arms: a store pointed at a database that was never
/// provisioned must say so, in a type a caller cannot confuse with "this request is already on file".
/// </summary>
/// <remarks>
/// <para>
/// The two conditions have opposite correct responses. "Already on file" means the request is safely
/// stored and the caller should stop. "Not provisioned" means nothing was stored and the caller must
/// re-file once the schema is repaired. While both raised <see cref="InvalidOperationException"/>, a
/// caller behaving correctly on the first reading would discard every erasure request filed against a
/// mis-provisioned database, and nothing anywhere would report it.
/// </para>
/// <para>
/// Each arm below is paired. The safety half asserts the fault is not reported as a duplicate; the
/// liveness half asserts a provisioned store still starts and still stores — without it, a store that
/// raised a provisioning fault unconditionally, or a host that never started at all, would pass.
/// </para>
/// </remarks>
internal static class ErasureProvisioningFaultFixtures
{
	internal static ErasureRequest CreateRequest() => new()
	{
		RequestId = Guid.NewGuid(),
		DataSubjectId = "subject-" + Guid.NewGuid().ToString("N"),
		IdType = DataSubjectIdType.UserId,
		LegalBasis = ErasureLegalBasis.DataSubjectRequest,
		RequestedBy = "tester",
	};

	/// <summary>
	/// Asserts the property that actually protects the caller: a provisioning fault must be
	/// distinguishable from a duplicate, which means it must not be an
	/// <see cref="InvalidOperationException"/> at all.
	/// </summary>
	internal static void ShouldNotBeConfusableWithADuplicate(this ErasureStoreNotProvisionedException thrown) =>
		thrown.ShouldNotBeAssignableTo<InvalidOperationException>(
			"a provisioning fault must sit outside the InvalidOperationException hierarchy. Inside it, a "
			+ "caller catching InvalidOperationException to handle a duplicate would read 'this database "
			+ "was never provisioned' as 'this request is already on file', and drop a request that was "
			+ "never stored.");
}

/// <summary>Postgres arm.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
public sealed class PostgresErasureProvisioningFaultShould
{
	private readonly PostgresFixture _fixture;
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public PostgresErasureProvisioningFaultShould(PostgresFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ReportAnUnprovisionedStoreAsAProvisioningFaultNotADuplicate()
	{
		// SAFETY. Constructed directly, so this exercises the first-use floor that host-less consumers
		// still rely on now that startup verification has moved to the hosted service.
		var store = CreateStore(autoCreateSchema: false);

		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => store.SaveRequestAsync(
				ErasureProvisioningFaultFixtures.CreateRequest(),
				DateTimeOffset.UtcNow.AddDays(7),
				TestContext.Current.CancellationToken));

		thrown.ShouldNotBeConfusableWithADuplicate();
		thrown.TableName.ShouldNotBeNullOrWhiteSpace(
			"the operator has to be told which table to provision; a bare 'the schema is stale' sends "
			+ "them to diff it by hand.");
	}

	[Fact]
	public async Task StillStoreARequestWhenTheSchemaIsPresent()
	{
		// LIVENESS. A store that raised a provisioning fault unconditionally would satisfy the arm above.
		var store = CreateStore(autoCreateSchema: true);
		var request = ErasureProvisioningFaultFixtures.CreateRequest();

		await store.SaveRequestAsync(
			request, DateTimeOffset.UtcNow.AddDays(7), TestContext.Current.CancellationToken);

		var status = await store.GetStatusAsync(request.RequestId, TestContext.Current.CancellationToken);
		status.ShouldNotBeNull();
	}

	[Fact]
	public async Task FailHostStartupRatherThanTheFirstErasureRequest()
	{
		// SAFETY, through the real registration path: the hosted service the DI extension registers must
		// actually resolve, and must actually refuse to start. Verification belongs at startup precisely
		// so that a deployment fault never arrives as the failure of one erasure request.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - this lock must never be skipped.");

		await using var provider = BuildProvider(autoCreateSchema: false);

		_ = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => StartHostedServicesAsync(provider, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task StartCleanlyAndServeWhenTheSchemaIsPresent()
	{
		// LIVENESS for the arm above. A host that refused to start on every configuration, or a
		// registration that contributed no validator at all, would both pass a startup-refusal assertion
		// on its own.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - this lock must never be skipped.");

		await using var provider = BuildProvider(autoCreateSchema: true);

		provider.GetServices<IErasureSchemaValidator>().ShouldNotBeEmpty(
			"the registration must actually contribute a validator, or startup verification is a no-op.");

		await StartHostedServicesAsync(provider, TestContext.Current.CancellationToken);

		var store = provider.GetRequiredService<IErasureStore>();
		var request = ErasureProvisioningFaultFixtures.CreateRequest();
		await store.SaveRequestAsync(
			request, DateTimeOffset.UtcNow.AddDays(7), TestContext.Current.CancellationToken);

		var status = await store.GetStatusAsync(request.RequestId, TestContext.Current.CancellationToken);
		status.ShouldNotBeNull();
	}

	private static async Task StartHostedServicesAsync(
		IServiceProvider provider,
		CancellationToken cancellationToken)
	{
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(cancellationToken);
		}
	}

	private ServiceProvider BuildProvider(bool autoCreateSchema)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The store pseudonymizes data-subject ids, and the hasher refuses to fall back to an unkeyed
		// hash, so a pepper is part of the minimum real composition — same as any consumer's.
		_ = services.Configure<DataSubjectHashingOptions>(
			o => o.Pepper = "erasure-provisioning-lock-pepper-0123456789ab");
		_ = services.AddPostgresErasureStore(options =>
		{
			options.ConnectionString = _fixture.ConnectionString;
			options.SchemaName = "compliance";
			options.RequestsTableName = TableName("erasure_requests_prov", autoCreateSchema);
			options.CertificatesTableName = TableName("erasure_certificates_prov", autoCreateSchema);
			options.CommandTimeoutSeconds = 30;
			options.AutoCreateSchema = autoCreateSchema;
		});

		return services.BuildServiceProvider();
	}

	private PgStore CreateStore(bool autoCreateSchema)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - the catalogue lookup under test is answered by the "
			+ "engine and this lock must never be skipped.");

		return new PgStore(
			Microsoft.Extensions.Options.Options.Create(new PgStoreOptions
			{
				ConnectionString = _fixture.ConnectionString,
				SchemaName = "compliance",
				RequestsTableName = TableName("erasure_requests_direct", autoCreateSchema),
				CertificatesTableName = TableName("erasure_certificates_direct", autoCreateSchema),
				CommandTimeoutSeconds = 30,
				AutoCreateSchema = autoCreateSchema,
			}),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<PgStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
	}

	private string TableName(string prefix, bool autoCreateSchema) =>
		prefix + "_" + _suffix + (autoCreateSchema ? "_ok" : "_missing");
}

/// <summary>SQL Server arm.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
public sealed class SqlServerErasureProvisioningFaultShould
{
	private readonly SqlServerFixture _fixture;
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public SqlServerErasureProvisioningFaultShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ReportAnUnprovisionedStoreAsAProvisioningFaultNotADuplicate()
	{
		var store = CreateStore(autoCreateSchema: false);

		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => store.SaveRequestAsync(
				ErasureProvisioningFaultFixtures.CreateRequest(),
				DateTimeOffset.UtcNow.AddDays(7),
				TestContext.Current.CancellationToken));

		thrown.ShouldNotBeConfusableWithADuplicate();
		thrown.TableName.ShouldNotBeNullOrWhiteSpace(
			"the operator has to be told which table to provision; a bare 'the schema is stale' sends "
			+ "them to diff it by hand.");
	}

	[Fact]
	public async Task StillStoreARequestWhenTheSchemaIsPresent()
	{
		var store = CreateStore(autoCreateSchema: true);
		var request = ErasureProvisioningFaultFixtures.CreateRequest();

		await store.SaveRequestAsync(
			request, DateTimeOffset.UtcNow.AddDays(7), TestContext.Current.CancellationToken);

		var status = await store.GetStatusAsync(request.RequestId, TestContext.Current.CancellationToken);
		status.ShouldNotBeNull();
	}

	[Fact]
	public async Task FailHostStartupRatherThanTheFirstErasureRequest()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available - this lock must never be skipped.");

		await using var provider = BuildProvider(autoCreateSchema: false);

		_ = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => StartHostedServicesAsync(provider, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task StartCleanlyAndServeWhenTheSchemaIsPresent()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available - this lock must never be skipped.");

		await using var provider = BuildProvider(autoCreateSchema: true);

		provider.GetServices<IErasureSchemaValidator>().ShouldNotBeEmpty(
			"the registration must actually contribute a validator, or startup verification is a no-op.");

		await StartHostedServicesAsync(provider, TestContext.Current.CancellationToken);

		var store = provider.GetRequiredService<IErasureStore>();
		var request = ErasureProvisioningFaultFixtures.CreateRequest();
		await store.SaveRequestAsync(
			request, DateTimeOffset.UtcNow.AddDays(7), TestContext.Current.CancellationToken);

		var status = await store.GetStatusAsync(request.RequestId, TestContext.Current.CancellationToken);
		status.ShouldNotBeNull();
	}

	private static async Task StartHostedServicesAsync(
		IServiceProvider provider,
		CancellationToken cancellationToken)
	{
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(cancellationToken);
		}
	}

	private ServiceProvider BuildProvider(bool autoCreateSchema)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The store pseudonymizes data-subject ids, and the hasher refuses to fall back to an unkeyed
		// hash, so a pepper is part of the minimum real composition — same as any consumer's.
		_ = services.Configure<DataSubjectHashingOptions>(
			o => o.Pepper = "erasure-provisioning-lock-pepper-0123456789ab");
		_ = services.AddSqlServerErasureStore(options =>
		{
			options.ConnectionString = _fixture.ConnectionString;
			options.SchemaName = "compliance";
			options.RequestsTableName = TableName("ErasureRequestsProv", autoCreateSchema);
			options.CertificatesTableName = TableName("ErasureCertificatesProv", autoCreateSchema);
			options.CommandTimeoutSeconds = 30;
			options.AutoCreateSchema = autoCreateSchema;
		});

		return services.BuildServiceProvider();
	}

	private SqlStore CreateStore(bool autoCreateSchema)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available - the catalogue lookup under test is answered by the "
			+ "engine and this lock must never be skipped.");

		return new SqlStore(
			Microsoft.Extensions.Options.Options.Create(new SqlStoreOptions
			{
				ConnectionString = _fixture.ConnectionString,
				SchemaName = "compliance",
				RequestsTableName = TableName("ErasureRequestsDirect", autoCreateSchema),
				CertificatesTableName = TableName("ErasureCertificatesDirect", autoCreateSchema),
				CommandTimeoutSeconds = 30,
				AutoCreateSchema = autoCreateSchema,
			}),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
	}

	private string TableName(string prefix, bool autoCreateSchema) =>
		prefix + _suffix + (autoCreateSchema ? "Ok" : "Missing");
}
