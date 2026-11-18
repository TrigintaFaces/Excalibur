// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Excalibur.Testing.Conformance;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Runs the fencing-contract conformance arms against the real in-memory provider.
/// </summary>
/// <remarks>
/// <para>
/// This is the only deriving suite that needs no infrastructure, so it is the one arm set that runs on every
/// unit CI pass. It is also the least load-bearing of the seven: the in-memory counter is process-local, so
/// it cannot exhibit the store-specific inversions the distributed providers shipped. Its value is that a
/// change to the shared conformance arms themselves is caught here immediately, without Docker.
/// </para>
/// <para>
/// The provider is resolved through the public registration rather than constructed, so the suite exercises
/// the path a consumer takes.
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class InMemoryFencingTokenProviderConformanceShould : FencingTokenProviderConformanceTestKit, IAsyncLifetime
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

	private ServiceProvider? _services;

	/// <inheritdoc/>
	protected override Task<IFencingTokenProvider> CreateProviderAsync()
	{
		var services = new ServiceCollection();
		_ = services.AddInMemoryFencingTokenProvider();
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
