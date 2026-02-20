using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Tests.Core.Health;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotStoreHealthCheckShould
{
	private readonly ISnapshotStore _snapshotStore;
	private readonly SnapshotStoreHealthCheck _sut;

	// Mirror the internal probe constants from SnapshotStoreHealthCheck
	private const string ProbeAggregateId = "__health_probe__";
	private const string ProbeAggregateType = "__health__";

	public SnapshotStoreHealthCheckShould()
	{
		_snapshotStore = A.Fake<ISnapshotStore>();
		_sut = new SnapshotStoreHealthCheck(_snapshotStore);
	}

	[Fact]
	public async Task ReturnHealthy_WhenSnapshotStoreReturnsNull()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
#pragma warning restore CA2012

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("reachable");
		result.Description.ShouldContain("no probe snapshot");
	}

	[Fact]
	public async Task ReturnHealthy_WhenSnapshotStoreReturnsSnapshot()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
#pragma warning disable CA2012
		A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));
#pragma warning restore CA2012

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("reachable");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenSnapshotStoreThrows()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _snapshotStore.GetLatestSnapshotAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(Task.FromException<ISnapshot?>(new TimeoutException("timeout"))));
#pragma warning restore CA2012

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("unreachable");
		result.Exception.ShouldBeOfType<TimeoutException>();
	}

	[Fact]
	public void ThrowOnNullSnapshotStore()
	{
		Should.Throw<ArgumentNullException>(() => new SnapshotStoreHealthCheck(null!));
	}
}
