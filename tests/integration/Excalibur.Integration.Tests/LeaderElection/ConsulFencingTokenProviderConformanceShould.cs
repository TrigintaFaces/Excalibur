// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Consul;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Consul;

using Excalibur.Testing.Conformance;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the Consul provider on a real agent.
/// </summary>
/// <remarks>
/// Consul's mint is a Check-And-Set write against the KV key's <c>ModifyIndex</c>, and its never-issued
/// state is an absent key -- a key that exists but cannot be parsed is a third, distinct outcome that must
/// throw rather than read as a value. A mocked client reproduces none of the three. Each test-class instance
/// gets its own key prefix so concurrently executing arms cannot share a counter key.
/// </remarks>
[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Consul")]
public sealed class ConsulFencingTokenProviderConformanceShould : FencingTokenProviderConformanceTestKit, IAsyncLifetime
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

	private readonly ConsulContainerFixture _fixture;
	private readonly string _keyPrefix = "fenceconf-" + Guid.NewGuid().ToString("N");
	private ServiceProvider? _services;

	public ConsulFencingTokenProviderConformanceShould(ConsulContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a container");

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
			config.Address = new Uri(_fixture.ConsulAddress)));
		_ = services.Configure<ConsulLeaderElectionOptions>(o => o.KeyPrefix = _keyPrefix);
		_ = services.AddConsulFencingTokenProvider();
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
