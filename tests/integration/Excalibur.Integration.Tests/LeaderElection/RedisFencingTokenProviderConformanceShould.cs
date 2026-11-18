// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using StackExchange.Redis;

using Excalibur.Testing.Conformance;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the Redis provider on a real server.
/// </summary>
/// <remarks>
/// Redis represents the never-issued state as an absent key, and its mint is a server-side <c>INCR</c>. A
/// mocked database returns whatever it was stubbed with for both, so neither the absent-mark arms nor the
/// concurrency arm mean anything without a real server.
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Redis")]
public sealed class RedisFencingTokenProviderConformanceShould
	: FencingTokenProviderConformanceTestKit, IAsyncLifetime, IClassFixture<RedisContainerFixture>
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

	private readonly RedisContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;
	private ServiceProvider? _services;

	public RedisFencingTokenProviderConformanceShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override async Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a container");

		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConnectionMultiplexer>(_connection);
		_ = services.AddRedisFencingTokenProvider();
		_services = services.BuildServiceProvider();

		return _services.GetRequiredService<IFencingTokenProvider>();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A Redis string can hold "0", and nothing in the provider prevents one existing: the mint is a
	/// server-side <c>INCR</c>, which initialises a missing key to 1 and never writes zero, so the state
	/// arrives from outside this provider -- a restore, a migration, an external <c>SET</c> against the
	/// consumer's own Redis, or a counter written by an older build. The substrate can express it, which
	/// is what this declaration is about; whether our own code writes it is a different question.
	/// </remarks>
	protected override bool StoreCanHoldAnUnadvancedMark => true;

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The literal below duplicates <c>RedisFencingTokenProvider.KeyPrefix</c> across an assembly
	/// boundary with nothing tying the two together. If that constant ever becomes an option -- which
	/// the MongoDB and Consul providers have already done for their own prefixes -- this override would
	/// keep writing the old key while the provider read the configured one, and the arm would silently
	/// revert to exercising the ABSENT state. That reverts green: the base arm asserts the read is null,
	/// and a key the provider never looks at reads null too.
	/// </para>
	/// <para>
	/// So the write is preceded by a ROUND TRIP that fails loudly if the key is wrong. Asserting null
	/// after the write would not do it -- null is what a wrong key produces as well, which is the whole
	/// reason the drift is invisible.
	/// </para>
	/// </remarks>
	protected override async Task CreateUnadvancedMarkAsync(string resourceId)
	{
		_connection.ShouldNotBeNull("the provider is created before this runs, so the connection exists");

		var database = _connection.GetDatabase();
		var key = "fencing:" + resourceId;

		// Prove this is the key the provider READS before writing the state under test. A positive value
		// is the only observation that discriminates: the provider reports a real mark for it, and
		// reports null both for an unadvanced mark AND for a key it never looks at.
		_ = await database.StringSetAsync(key, 7L).ConfigureAwait(false);

		var observed = await Provider
			.GetTokenAsync(resourceId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		observed.ShouldBe(
			7L,
			"this override must write the SAME key the provider reads, or the arm it feeds exercises the "
			+ "absent state instead of the unadvanced one -- and in the absent state the fixed code and "
			+ "the broken code agree, so the arm would pass while testing nothing. The source of this "
			+ "literal is RedisFencingTokenProvider's KeyPrefix constant.");

		// Now the state under test: a key that EXISTS and holds zero.
		_ = await database.StringSetAsync(key, 0L).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_services is not null)
		{
			await _services.DisposeAsync().ConfigureAwait(false);
			_services = null;
		}

		if (_connection is not null)
		{
			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}
	}
}
