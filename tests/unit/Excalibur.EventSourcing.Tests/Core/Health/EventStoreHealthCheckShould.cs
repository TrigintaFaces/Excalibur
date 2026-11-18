using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Tests.Core.Health;

/// <summary>
/// The check has exactly TWO verdicts and the verdict is decided by the store.
/// </summary>
/// <remarks>
/// <para>
/// A third verdict used to exist. The check probed through <c>IEventStore.LoadAsync</c>, which binds a
/// tenant term, so on a tenant-routing deployment with no default shard it could not resolve a store at
/// all — and it reported <c>Degraded</c> with "nothing about the store's schema was verified". That is a
/// deterministic property of the configuration, not of the store, so the check reported a non-verdict on
/// every invocation, forever, on a composition a consumer reaches without doing anything unusual.
/// </para>
/// <para>
/// The arm asserting that <c>Degraded</c> is DELETED rather than flipped, and its own stated reasoning is
/// why: it argued that Unhealthy would be wrong because "a permanently-red check gets deleted from the
/// readiness probe, leaving nothing". The author had already worked out that the check could never go
/// green on that deployment, and answered it by softening the status instead of removing the dependency
/// that caused it. A check that can never report Healthy on a legitimate deployment is the defect; a
/// softer status for it is the same defect in a costume, and Degraded is the status an operator learns to
/// ignore.
/// </para>
/// <para>
/// The probe is now the global-stream read, which binds the SAME eleven columns with no tenant or shard
/// dependency — so the configuration can no longer produce a non-verdict, and the arm for one cannot be
/// written.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreHealthCheckShould
{
	private readonly IGlobalStreamQuery _globalStream;
	private readonly EventStoreHealthCheck _sut;

	public EventStoreHealthCheckShould()
	{
		_globalStream = A.Fake<IGlobalStreamQuery>();
		_sut = new EventStoreHealthCheck(_globalStream);
	}

	[Fact]
	public async Task ReportHealthyWhenTheStoreServesAReadOfItsEventTable()
	{
		Probe().Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		result.Status.ShouldBe(HealthStatus.Healthy);
		_ = result.Description.ShouldNotBeNull();

		// The claim is that a READ OF THE EVENT TABLE was served, which a bare connectivity probe does
		// not establish. So the description must name the table, and no fault may be attached.
		result.Description.ShouldContain("event table");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task ProbeFromTheStartOfTheStreamAndReadOnlyOneRow()
	{
		Probe().Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		_ = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// The select list is bound at parse time, so one row detects a missing table or column against an
		// EMPTY store. This arm pins both halves of that: reading from the start needs no data to exist,
		// and maxCount 1 keeps a health endpoint off a full scan of the stream.
		A.CallTo(() => _globalStream.ReadAllAsync(
				GlobalStreamPosition.Start,
				1,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportUnhealthyWhenTheStoreRefusesAReadItShouldHaveServed()
	{
		Probe().Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
			Task.FromException<IReadOnlyList<StoredEvent>>(new InvalidOperationException("connection failed"))));

		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		result.Status.ShouldBe(HealthStatus.Unhealthy);

		// "unreachable" was the wrong diagnosis as well as a weak assertion: a refused read may mean the
		// table is missing a column the read statement binds, which is not an unreachable database, and
		// the description must not send an operator to check the network.
		result.Description.ShouldContain("refused a read");
		result.Description.ShouldContain("missing a column");
		result.Exception!.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task PropagateCancellationRatherThanReportingAHostShutdownAsUnhealthy()
	{
		// A rolling deploy cancels in-flight probes. Converting that into Unhealthy pages someone for a
		// clean shutdown, so cancellation must leave the check rather than become a verdict.
		Probe().Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
			Task.FromException<IReadOnlyList<StoredEvent>>(new OperationCanceledException())));

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));
	}

	[Fact]
	public void RefuseToBeConstructedWithoutAGlobalStreamReader() =>
		Should.Throw<ArgumentNullException>(
			() => new EventStoreHealthCheck(null!),
			"the reader is the only thing this check probes, so there is no degraded mode to fall back to.");

#pragma warning disable CA2012 // deliberately configuring a ValueTask-returning member on a fake
	private FakeItEasy.Configuration.IReturnValueArgumentValidationConfiguration<ValueTask<IReadOnlyList<StoredEvent>>> Probe() =>
		A.CallTo(() => _globalStream.ReadAllAsync(
			A<GlobalStreamPosition>._,
			A<int>._,
			A<CancellationToken>._));
#pragma warning restore CA2012
}
