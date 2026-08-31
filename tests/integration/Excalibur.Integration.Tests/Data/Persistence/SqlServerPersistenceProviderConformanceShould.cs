// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Excalibur.Data;
using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;
using Excalibur.Data.SqlServer.Persistence;
using Excalibur.Integration.Tests.Data;
using Excalibur.Testing.Conformance;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqlServerPersistenceProvider"/> (the
/// <c>AddSqlServerPersistence</c> DI-wired implementation of <see cref="IPersistenceProvider"/>) using the
/// Persistence Provider Conformance Test Kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass.
/// </para>
/// <para>
/// <b>Constructed directly, not resolved from a container.</b> The kit disposes the provider it is given
/// once per arm, so a container-owned singleton would be disposed by the first arm and reused after
/// disposal by the next. Direct construction gives each arm its own instance. This is a lifetime
/// decision, not a statement about the wiring: that <c>AddSqlServerPersistence</c> resolves the provider
/// is covered separately by <c>SqlServerPersistenceRegistrationShould</c>, which builds a real service
/// provider through the production registration path.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "PROVIDER")]
public sealed class SqlServerPersistenceProviderConformanceShould : PersistenceProviderConformanceTestKit, IClassFixture<SqlServerContainerFixture>
{
	private readonly SqlServerContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerPersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerPersistenceProviderConformanceShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "SQL";

	/// <inheritdoc/>
	/// <remarks>
	/// One specification for every deriver: the suite configures this name on the provider and
	/// expects the provider to report it back. An expectation set to whatever the provider happens
	/// to return cannot detect a provider that ignores its configured name -- it certifies it.
	/// </remarks>

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Type> RequiredCapabilities =>
		[typeof(IPersistenceProviderHealth), typeof(IPersistenceProviderConnection), typeof(IPersistenceProviderTransaction)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new SqlServerPersistenceOptions
		{
			Name = providerName,
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true },
		});

		var metrics = new SqlServerPersistenceMetrics(options);
		// A deterministic stand-in for the retry policy: these arms certify the provider's own behaviour,
		// and a real backoff would make them timing-dependent. The policy the production registration
		// supplies is asserted by SqlServerPersistenceRegistrationShould.
		var retryPolicy = A.Fake<IDataRequestRetryPolicy>();
		_ = A.CallTo(() => retryPolicy.MaxRetryAttempts).Returns(3);
		_ = A.CallTo(() => retryPolicy.BaseRetryDelay).Returns(TimeSpan.FromMilliseconds(50));
		_ = A.CallTo(() => retryPolicy.ShouldRetry(A<Exception>._)).Returns(false);

		return new SqlServerPersistenceProvider(
			options,
			NullLogger<SqlServerPersistenceProvider>.Instance,
			NullLoggerFactory.Instance,
			metrics,
			retryPolicy);
	}

	// -------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped.
	// -------------------------------------------------------------------------------------------------

	[Fact] public void Provider_ShouldHaveNonNullName_Test() => Provider_ShouldHaveNonNullName();
	[Fact] public void Provider_ShouldHaveExpectedName_Test() => Provider_ShouldHaveExpectedName();
	[Fact] public void Provider_NameShouldRoundTripEveryConfiguredName_Test() => Provider_NameShouldRoundTripEveryConfiguredName();
	[Fact] public Task Provider_NameShouldBeStableAcrossLifecycle_Test() => Provider_NameShouldBeStableAcrossLifecycle();
	[Fact] public void Provider_ShouldHaveNonNullProviderType_Test() => Provider_ShouldHaveNonNullProviderType();
	[Fact] public void Provider_ShouldHaveExpectedProviderType_Test() => Provider_ShouldHaveExpectedProviderType();
	[Fact] public void Provider_ShouldHaveNonNullConnectionString_Test() => Provider_ShouldHaveNonNullConnectionString();
	[Fact] public void Provider_ShouldHaveNonNullRetryPolicy_Test() => Provider_ShouldHaveNonNullRetryPolicy();
	[Fact] public void CreateTransactionScope_ShouldReturnNonNullScope_Test() => CreateTransactionScope_ShouldReturnNonNullScope();
	[Fact] public void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope_Test() => CreateTransactionScope_WithIsolationLevel_ShouldReturnScope();
	[Fact] public void CreateTransactionScope_WithTimeout_ShouldReturnScope_Test() => CreateTransactionScope_WithTimeout_ShouldReturnScope();
	[Fact] public Task GetMetricsAsync_ShouldReturnNonNullDictionary_Test() => GetMetricsAsync_ShouldReturnNonNullDictionary();
	[Fact] public Task TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize_Test() => TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize();
	[Fact] public Task GetMetricsAsync_ShouldContainProviderKey_Test() => GetMetricsAsync_ShouldContainProviderKey();
	[Fact] public void Dispose_ShouldNotThrow_Test() => Dispose_ShouldNotThrow();
	[Fact] public void Dispose_CalledMultipleTimes_ShouldNotThrow_Test() => Dispose_CalledMultipleTimes_ShouldNotThrow();
	[Fact] public Task DisposeAsync_ShouldNotThrow_Test() => DisposeAsync_ShouldNotThrow();
	[Fact] public void IsAvailable_AfterDispose_ShouldBeFalse_Test() => IsAvailable_AfterDispose_ShouldBeFalse();
	[Fact] public void Provider_ShouldOfferRequiredCapabilities_Test() => Provider_ShouldOfferRequiredCapabilities();
	[Fact] public void Provider_ShouldImplementIDisposable_Test() => Provider_ShouldImplementIDisposable();
	[Fact] public void Provider_ShouldImplementIAsyncDisposable_Test() => Provider_ShouldImplementIAsyncDisposable();
	[Fact] public Task ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted_Test() => ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
	[Fact] public void ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers_Test() => ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers();

	/// <inheritdoc/>
	/// <remarks>
	/// Same probe shape as the Postgres deriver, and the enlistment works for a different reason worth
	/// naming: this provider cannot flow a SqlTransaction onto its Dapper commands, so it opens the
	/// connection inside an ambient <c>TransactionScope</c> and relies on SqlConnection auto-enlisting.
	/// The probe's command is created on that same connection, so it participates the same way a
	/// production request does -- which is the point: if enlistment ever stops happening, this arm is
	/// what notices, and the batch half-commits silently in the meantime.
	/// </remarks>
	protected override async Task<(IReadOnlyList<IDataRequest<IDbConnection, object>> Requests, Func<Task<bool>> FirstEffectVisibleAsync, Func<Task<bool>> FirstEffectPersistsWhenBatchSucceedsAsync)?>
		CreateBatchAtomicityProbeAsync(ISqlPersistenceProvider provider)
	{
		await provider.InitializeAsync(
			new SqlServerPersistenceOptions { Name = provider.Name, ConnectionString = _fixture.ConnectionString },
			CancellationToken.None).ConfigureAwait(false);

		var table = "batch_atomicity_" + Guid.NewGuid().ToString("N");
		await ExecuteAsync($"CREATE TABLE [{table}] (id int primary key)").ConfigureAwait(false);

		return (
			[
				new SqlProbeRequest($"INSERT INTO [{table}] (id) VALUES (1)"),
				new SqlProbeRequest($"INSERT INTO [{table}_missing] (id) VALUES (2)"),
			],
			() => AnyRowsAsync(table),
			async () =>
			{
				// Liveness: an all-valid batch must leave its row behind. Without this the atomicity
				// assertion is satisfied by a probe whose first request never took effect at all.
				var live = "batch_liveness_" + Guid.NewGuid().ToString("N");
				await ExecuteAsync($"CREATE TABLE [{live}] (id int primary key)").ConfigureAwait(false);
				_ = await provider.ExecuteBatchAsync(
					[new SqlProbeRequest($"INSERT INTO [{live}] (id) VALUES (1)")],
					CancellationToken.None).ConfigureAwait(false);
				return await AnyRowsAsync(live).ConfigureAwait(false);
			});
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		#pragma warning disable CA2100 // locally generated hex GUID table name; no caller input reaches this
		await using var command = new SqlCommand(sql, connection);
		#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private async Task<bool> AnyRowsAsync(string table)
	{
		// A SEPARATE connection: reading on the batch's own would see its uncommitted row and report a
		// rollback that never happened.
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		#pragma warning disable CA2100 // locally generated hex GUID table name
		await using var command = new SqlCommand($"SELECT COUNT(*) FROM [{table}]", connection);
		#pragma warning restore CA2100
		return Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false),
			System.Globalization.CultureInfo.InvariantCulture) > 0;
	}

	/// <summary>A data request that runs one statement, used only by the batch-atomicity probe.</summary>
	private sealed class SqlProbeRequest : IDataRequest<IDbConnection, object>
	{
		public SqlProbeRequest(string sql)
		{
			Command = new CommandDefinition(sql);
			ResolveAsync = connection =>
			{
				using var command = connection.CreateCommand();
				command.CommandText = sql;
				_ = command.ExecuteNonQuery();
				return Task.FromResult<object>(0);
			};
		}

		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => nameof(SqlProbeRequest);
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; } = new();
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
	}
}
