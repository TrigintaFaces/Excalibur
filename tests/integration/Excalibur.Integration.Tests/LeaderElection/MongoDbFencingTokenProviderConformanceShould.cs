// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;

using MongoDB.Driver;

using Excalibur.Testing.Conformance;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the MongoDB provider on a real server.
/// </summary>
/// <remarks>
/// The counter is a document and the mint is a server-side <c>findAndModify</c> upsert, neither of which a
/// mocked collection executes -- a mocked driver has already been observed in this repository to certify a
/// provider that threw on every real write. Each test-class instance gets its own database so that
/// concurrently executing arms cannot share a counter collection.
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbFencingTokenProviderConformanceShould
	: FencingTokenProviderConformanceTestKit, IAsyncLifetime, IClassFixture<MongoDbContainerFixture>
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

	private readonly MongoDbContainerFixture _fixture;
	private readonly string _databaseName = "fenceconf_" + Guid.NewGuid().ToString("N");
	private ServiceProvider? _services;

	public MongoDbFencingTokenProviderConformanceShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a container");

		var services = new ServiceCollection();
		_ = services.AddSingleton<IMongoClient>(new MongoClient(_fixture.ConnectionString));
		_ = services.Configure<MongoDbLeaderElectionOptions>(o =>
		{
			o.ConnectionString = _fixture.ConnectionString;
			o.DatabaseName = _databaseName;
			o.CollectionName = "leader_elections";
		});
		_ = services.AddMongoDbFencingTokenProvider();
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
