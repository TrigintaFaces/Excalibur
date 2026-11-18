// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Postgres;

using Npgsql;

using Excalibur.Testing.Conformance;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the Postgres provider on a real server.
/// </summary>
/// <remarks>
/// <para>
/// A mocked connection cannot run a server-side sequence, so it would certify whatever the stub was told to
/// return -- which is how the suite these arms replace came to be unable to fail. The never-issued arms are
/// the load-bearing ones here: Postgres represents "never drawn" as a NULL <c>last_value</c> in
/// <c>pg_sequences</c>, and only a real server produces that row.
/// </para>
/// <para>
/// Docker availability is asserted rather than tolerated, so an absent container fails every arm loudly
/// instead of passing by not running.
/// </para>
/// </remarks>
[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Postgres")]
public sealed class PostgresFencingTokenProviderConformanceShould : FencingTokenProviderConformanceTestKit, IAsyncLifetime
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

	private readonly PostgresContainerFixture _fixture;
	private ServiceProvider? _services;

	public PostgresFencingTokenProviderConformanceShould(PostgresContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// A Postgres SEQUENCE exists before its first <c>nextval</c>, and the issue path creates it and draws
	/// from it as two statements, so an interruption between the two is reachable. The row is then present in
	/// <c>pg_sequences</c> with a NULL <c>last_value</c> and a non-NULL <c>start_value</c>, which is exactly
	/// the pair a fence can read the wrong member of.
	/// </remarks>
	protected override bool StoreCanHoldAnUnadvancedMark => true;

	/// <inheritdoc/>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Security",
		"CA2100:Review SQL queries for security vulnerabilities",
		Justification = "The only interpolated token is the sequence name: 'fencing_' + lowercase-hex "
			+ "SHA-256 of a resource id this test generated, i.e. exclusively [0-9a-f], so it cannot carry "
			+ "SQL injection. Postgres DDL identifiers cannot be parameterized, and the provider inlines "
			+ "the same provably-safe name under the same suppression.")]
	protected override async Task CreateUnadvancedMarkAsync(string resourceId)
	{
		// The same name the provider derives, so the arm reads the artifact the provider would read: SHA-256
		// hex, lower-cased and cut to 55 characters, which with the prefix reaches the 63-character
		// identifier limit.
		var sequenceName = "fencing_"
			+ Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resourceId)))[..55];

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// Byte-for-byte the DDL half of the provider's own issue statement, minus the nextval that would
		// advance it. A sequence created any other way would not be the artifact the provider reads.
		await using var command = new NpgsqlCommand($"CREATE SEQUENCE IF NOT EXISTS {sequenceName};", connection);

		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a container");

		var services = new ServiceCollection();
		_ = services.AddPostgresFencingTokenProvider(_fixture.ConnectionString);
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
