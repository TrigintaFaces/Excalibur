using Excalibur.EventSourcing.Abstractions;
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
		result.Description.ShouldContain("reachable");
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
		result.Description.ShouldContain("unreachable");
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public void ThrowOnNullEventStore()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreHealthCheck(null!));
	}
}
