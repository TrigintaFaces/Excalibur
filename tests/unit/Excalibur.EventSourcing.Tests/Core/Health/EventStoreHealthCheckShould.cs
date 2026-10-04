using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Tests.Core.Health;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreHealthCheckShould
{
	private readonly IEventStore _eventStore;
	private readonly EventStoreHealthCheck _sut;

	// Mirror the internal probe constants from EventStoreHealthCheck
	private const string ProbeAggregateId = "__health_probe__";
	private const string ProbeAggregateType = "__health__";

	public EventStoreHealthCheckShould()
	{
		_eventStore = A.Fake<IEventStore>();
		_sut = new EventStoreHealthCheck(_eventStore);
	}

	[Fact]
	public async Task ReturnHealthy_WhenEventStoreIsReachable()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));
#pragma warning restore CA2012

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldNotBeNull();

		// Strengthened from a substring match on the word "reachable", which a bare connectivity probe
		// satisfies just as well. The check's claim is that it SERVED A READ OF THE EVENT TABLE, so the
		// description must say which table was read, and no fault may be attached.
		result.Description.ShouldContain("event table");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenEventStoreThrows()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Task.FromException<IReadOnlyList<StoredEvent>>(new InvalidOperationException("connection failed"))));
#pragma warning restore CA2012

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);

		// Strengthened: "unreachable" was the wrong diagnosis as well as a weak assertion. A read the
		// store refused may mean the table is missing a column the read statement binds, which is not an
		// unreachable database, and the description must not send an operator to check the network.
		result.Description.ShouldContain("refused a read");
		result.Description.ShouldContain("missing a column");
		result.Exception!.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task PropagateCancellation_RatherThanReportingAHostShutdownAsUnhealthy()
	{
		// A rolling deploy cancels in-flight probes. Converting that into Unhealthy pages someone for a
		// clean shutdown, so cancellation must leave the check rather than become a verdict.
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
				Task.FromException<IReadOnlyList<StoredEvent>>(new OperationCanceledException())));
#pragma warning restore CA2012

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));
	}

	[Fact]
	public async Task ReportDegraded_WhenNoTenantOrShardIsResolvableSoNothingWasVerified()
	{
		// A health check runs with no ambient tenant, so a tenant-routing store cannot resolve a shard
		// unless a default is configured. That is a configuration state, not a store fault: Unhealthy
		// would mark a working store broken (and a permanently-red check gets deleted from the readiness
		// probe, leaving nothing), while Healthy would claim a verification that never happened.
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(
			ProbeAggregateId,
			ProbeAggregateType,
			A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
				Task.FromException<IReadOnlyList<StoredEvent>>(
					new TenantShardNotFoundException("__untenanted__"))));
#pragma warning restore CA2012

		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		result.Status.ShouldBe(HealthStatus.Degraded,
			"an unprobeable store is neither verified nor known-broken, and reporting either would be a "
			+ "claim the check cannot support");
		result.Description.ShouldContain("nothing about the store's schema was verified");
	}

	[Fact]
	public void ThrowOnNullEventStore()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreHealthCheck(null!));
	}
}
