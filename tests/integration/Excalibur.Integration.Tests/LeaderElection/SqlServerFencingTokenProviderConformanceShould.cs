// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;

using Excalibur.Testing.Conformance;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the SQL Server provider on a real server.
/// </summary>
/// <remarks>
/// <para>
/// This provider is the reason the never-issued arms exist. <c>sys.sequences.current_value</c> is NOT NULL
/// and reports the START WITH value for a sequence nobody ever drew from, so a fence reading that column
/// answers a plausible low mark for a resource whose token was never issued -- and under the at-or-above
/// relation a low mark admits every token, including a fabricated one. The provider reads
/// <c>last_used_value</c>, which is nullable and can express never-drawn; these arms are what detects a
/// regression back to the other column.
/// </para>
/// <para>
/// Only a real server distinguishes the two columns, which is why this cannot be a unit arm.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerFencingTokenProviderConformanceShould : FencingTokenProviderConformanceTestKit, IAsyncLifetime
{
	/// <inheritdoc/>
	protected override CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

	[Fact]
	public override Task GetTokenAsync_ReturnsNull_WhenNoTokenWasEverIssued() => base.GetTokenAsync_ReturnsNull_WhenNoTokenWasEverIssued();

	[Theory]
	[InlineData(long.MinValue)]
	[InlineData(-1L)]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(2L)]
	[InlineData(long.MaxValue)]
	public override Task ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued(long presented) => base.ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued(presented);

	[Fact]
	public override Task UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken() => base.UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken();

	[Fact]
	public override Task EstablishedToken_IsStrictlyPositive() => base.EstablishedToken_IsStrictlyPositive();

	[Fact]
	public override Task GetTokenAsync_ReturnsTheEstablishedToken() => base.GetTokenAsync_ReturnsTheEstablishedToken();

	[Fact]
	public override Task EstablishToken_IsStrictlyMonotonicAcrossHandovers() => base.EstablishToken_IsStrictlyMonotonicAcrossHandovers();

	[Fact]
	public override Task ValidateTokenAsync_AdmitsThePinnedIncumbentToken() => base.ValidateTokenAsync_AdmitsThePinnedIncumbentToken();

	[Fact]
	public override Task ValidateTokenAsync_RefusesTheSupersededToken_AfterAHandover() => base.ValidateTokenAsync_RefusesTheSupersededToken_AfterAHandover();

	[Fact]
	public override Task ConcurrentEstablishes_YieldDistinctTokens() => base.ConcurrentEstablishes_YieldDistinctTokens();

	[Fact]
	public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

	private readonly SqlServerContainerFixture _fixture;
	private ServiceProvider? _services;

	public SqlServerFencingTokenProviderConformanceShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// A SQL Server SEQUENCE exists before it is drawn from, and the issue path creates it and draws from it
	/// in one batch with no enclosing transaction, so an interruption between the two is reachable.
	/// </remarks>
	protected override bool StoreCanHoldAnUnadvancedMark => true;

	/// <inheritdoc/>
	protected override async Task CreateUnadvancedMarkAsync(string resourceId)
	{
		// The same name the provider derives, so the arm reads the artifact the provider would read. A
		// different name would make the arm indistinguishable from the absent-store arms it exists to
		// complement.
		var sequenceName = "fencing_"
			+ Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resourceId)));

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(
			"DECLARE @sql nvarchar(max) = N'CREATE SEQUENCE ' + QUOTENAME(@name) "
			+ "+ N' AS bigint START WITH 1 INCREMENT BY 1'; EXEC sp_executesql @sql;",
			connection);
		_ = command.Parameters.AddWithValue("@name", sequenceName);

		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a container");

		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_fixture.ConnectionString);
		_services = services.BuildServiceProvider();

		return Task.FromResult(_services.GetRequiredService<IFencingTokenProvider>());
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_services is not null)
		{
			await _services.DisposeAsync().ConfigureAwait(false);
			_services = null;
		}
	}
}
