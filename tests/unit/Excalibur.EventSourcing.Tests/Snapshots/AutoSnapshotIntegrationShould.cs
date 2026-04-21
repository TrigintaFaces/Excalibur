// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Implementation;
using Microsoft.Extensions.Options;

#pragma warning disable CA1506 // Excessive class coupling -- test methods create repository with many DI parameters by design

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using AppendResult = Excalibur.EventSourcing.Abstractions.AppendResult;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// A.6 (y94bgd): Integration tests verifying auto-snapshot triggers from SaveAsync.
/// Tests: snapshot created on threshold, best-effort (failure doesn't fail save),
/// no-op when disabled, per-aggregate named options.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class AutoSnapshotIntegrationShould
{
	#region Test Aggregate

	internal sealed class SnapshotTestAggregate : AggregateRoot
	{
		public SnapshotTestAggregate() { }
		public SnapshotTestAggregate(string id) : base(id) { }

		public int Value { get; private set; }

		public void SetValue(int value)
		{
			RaiseEvent(new ValueSetEvent
			{
				AggregateId = Id,
				Version = Version,
				Value = value
			});
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is ValueSetEvent e)
			{
				Value = e.Value;
			}
		}
	}

	internal sealed class ValueSetEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(ValueSetEvent);
		public IDictionary<string, object>? Metadata { get; init; }
		public int Value { get; init; }
	}

	#endregion

	#region Helpers

	private static IEventStore CreateFakeEventStore(string aggregateId, params IDomainEvent[] existingEvents)
	{
		var store = A.Fake<IEventStore>();

		_ = A.CallTo(() => store.AppendAsync(
			A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(existingEvents.Length + 1, 1));

		return store;
	}

	private static IEventSerializer CreateFakeSerializer()
	{
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType(A<string>._))
			.Returns(typeof(ValueSetEvent));
		return serializer;
	}

	private static IOptionsMonitor<AutoSnapshotOptions> CreateOptions(Action<AutoSnapshotOptions> configure)
	{
		var options = new AutoSnapshotOptions();
		configure(options);
		var monitor = A.Fake<IOptionsMonitor<AutoSnapshotOptions>>();
		_ = A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		_ = A.CallTo(() => monitor.CurrentValue).Returns(options);
		return monitor;
	}

	#endregion

	[Fact]
	public async Task TriggerSnapshotWhenEventCountThresholdMet()
	{
		// Arrange
		var snapshotManager = A.Fake<ISnapshotManager>();
		var fakeSnapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.Returns(fakeSnapshot);

		var options = CreateOptions(o => o.EventCountThreshold = 3);
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: snapshotManager,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(1);
		aggregate.SetValue(2);
		aggregate.SetValue(3);

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- snapshot was created
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => snapshotManager.SaveSnapshotAsync(
			"agg-1", fakeSnapshot, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotTriggerSnapshotWhenBelowThreshold()
	{
		// Arrange
		var snapshotManager = A.Fake<ISnapshotManager>();
		var options = CreateOptions(o => o.EventCountThreshold = 100);
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: snapshotManager,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(1);
		aggregate.SetValue(2);

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- no snapshot created
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotFailSaveWhenSnapshotFails()
	{
		// Arrange -- best-effort: snapshot failure must NOT fail the save
		var snapshotManager = A.Fake<ISnapshotManager>();
		_ = A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Snapshot store unavailable"));

		var options = CreateOptions(o => o.EventCountThreshold = 1);
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: snapshotManager,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(42);
		aggregate.SetValue(43);

		// Act -- should NOT throw despite snapshot failure
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- events were still appended to event store
		A.CallTo(() => eventStore.AppendAsync(
			A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotEvaluateWhenNoThresholdsConfigured()
	{
		// Arrange -- all thresholds null = zero overhead path
		var snapshotManager = A.Fake<ISnapshotManager>();
		var options = CreateOptions(_ => { }); // all null
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: snapshotManager,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(1);
		aggregate.SetValue(2);

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- snapshot manager never called
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotEvaluateWhenSnapshotManagerIsNull()
	{
		// Arrange -- no snapshot manager = auto-snapshot disabled
		var options = CreateOptions(o => o.EventCountThreshold = 1);
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: null,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(1);
		aggregate.SetValue(2);

		// Act -- should complete without error
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert -- events still appended
		A.CallTo(() => eventStore.AppendAsync(
			A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task TriggerSnapshotWithCustomPolicy()
	{
		// Arrange
		var snapshotManager = A.Fake<ISnapshotManager>();
		var fakeSnapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.Returns(fakeSnapshot);

		var options = CreateOptions(o =>
			o.CustomPolicy = ctx => ctx.EventsSinceSnapshot >= 2);
		var eventStore = CreateFakeEventStore("agg-1");

		var repo = new EventSourcedRepository<SnapshotTestAggregate>(
			eventStore,
			CreateFakeSerializer(),
			id => new SnapshotTestAggregate(id),
			snapshotManager: snapshotManager,
			autoSnapshotOptions: options);

		var aggregate = new SnapshotTestAggregate("agg-1");
		aggregate.SetValue(1);
		aggregate.SetValue(2);

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		A.CallTo(() => snapshotManager.CreateSnapshotAsync(
			A<SnapshotTestAggregate>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
