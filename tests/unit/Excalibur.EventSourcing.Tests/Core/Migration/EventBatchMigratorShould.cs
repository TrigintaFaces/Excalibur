// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Migration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventBatchMigratorShould
{
	private readonly IEventStore _eventStore;
	private readonly MigrationOptions _options;
	private readonly EventBatchMigrator _sut;

	public EventBatchMigratorShould()
	{
		_eventStore = A.Fake<IEventStore>();
		_options = new MigrationOptions { BatchSize = 3 };
		_sut = new EventBatchMigrator(
			_eventStore,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<EventBatchMigrator>.Instance);
	}

	[Fact]
	public async Task MigrateAsync_ProcessEventsInBatches()
	{
		// Arrange
		var events = CreateStoredEvents(5);
		SetupEventStoreLoad("source-stream", events);
		SetupEventStoreAppendSuccess();

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(5);
		result.EventsSkipped.ShouldBe(0);
		result.StreamsMigrated.ShouldBe(1);
		result.IsDryRun.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task MigrateAsync_SkipEventsMatchingFilter()
	{
		// Arrange
		var events = CreateStoredEvents(4);
		SetupEventStoreLoad("source-stream", events);
		SetupEventStoreAppendSuccess();

		var plan = new MigrationPlan(
			"source-stream",
			"target-stream",
			EventFilter: e => e.Version % 2 == 0);

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
		result.EventsSkipped.ShouldBe(2);
	}

	[Fact]
	public async Task MigrateAsync_ApplyTransformFunction()
	{
		// Arrange
		var events = CreateStoredEvents(2);
		SetupEventStoreLoad("source-stream", events);
		SetupEventStoreAppendSuccess();

		var plan = new MigrationPlan(
			"source-stream",
			"target-stream",
			TransformFunc: e => e with { EventType = "Transformed" });

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task MigrateAsync_DryRunDoesNotWrite()
	{
		// Arrange
		_options.DryRun = true;
		var events = CreateStoredEvents(3);
		SetupEventStoreLoad("source-stream", events);

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(3);
		result.IsDryRun.ShouldBeTrue();
		A.CallTo(() => _eventStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
			A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MigrateAsync_RespectMaxEventsLimit()
	{
		// Arrange
		// MaxEvents=2 — the limit check includes events queued in the current batch,
		// so exactly 2 events are collected and flushed.
		_options.MaxEvents = 2;
		var events = CreateStoredEvents(5);
		SetupEventStoreLoad("source-stream", events);
		SetupEventStoreAppendSuccess();

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
	}

	[Fact]
	public async Task MigrateAsync_RecordErrorsOnAppendFailure()
	{
		// Arrange
		_options.ContinueOnError = true;
		var events = CreateStoredEvents(2);
		SetupEventStoreLoad("source-stream", events);

#pragma warning disable CA2012
		A.CallTo(() => _eventStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
			A<long>._, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(AppendResult.CreateFailure("write failed")));
#pragma warning restore CA2012

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task MigrateAsync_ThrowOnException_WhenContinueOnErrorIsFalse()
	{
		// Arrange
		_options.ContinueOnError = false;
		var events = CreateStoredEvents(2);
		SetupEventStoreLoad("source-stream", events);

#pragma warning disable CA2012
		A.CallTo(() => _eventStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
			A<long>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("failure"));
#pragma warning restore CA2012

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.MigrateAsync(plan, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateAsync_ContinueOnError_WhenEnabled()
	{
		// Arrange
		_options.ContinueOnError = true;
		var events = CreateStoredEvents(2);
		SetupEventStoreLoad("source-stream", events);

#pragma warning disable CA2012
		A.CallTo(() => _eventStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
			A<long>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("failure"));
#pragma warning restore CA2012

		var plan = new MigrationPlan("source-stream", "target-stream");

		// Act
		var result = await _sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task MigrateAsync_ThrowOnNullPlan()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.MigrateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CreatePlanAsync_CreatePlanFromSourcePattern()
	{
		// Arrange
		var options = new MigrationOptions
		{
			SourceStreamPattern = "Order-*",
			TargetStreamPrefix = "migrated_"
		};

		// Act
		var plans = await _sut.CreatePlanAsync(options, CancellationToken.None);

		// Assert
		plans.Count.ShouldBe(1);
		plans[0].SourceStream.ShouldBe("Order-*");
		plans[0].TargetStream.ShouldBe("migrated_Order-*");
	}

	[Fact]
	public async Task CreatePlanAsync_ReturnEmptyForNullPattern()
	{
		// Arrange
		var options = new MigrationOptions();

		// Act
		var plans = await _sut.CreatePlanAsync(options, CancellationToken.None);

		// Assert
		plans.ShouldBeEmpty();
	}

	[Fact]
	public async Task CreatePlanAsync_UseSourceStreamAsTarget_WhenNoPrefixSet()
	{
		// Arrange
		var options = new MigrationOptions { SourceStreamPattern = "Orders" };

		// Act
		var plans = await _sut.CreatePlanAsync(options, CancellationToken.None);

		// Assert
		plans.Count.ShouldBe(1);
		plans[0].TargetStream.ShouldBe("Orders");
	}

	[Fact]
	public async Task CreatePlanAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.CreatePlanAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new MigrationOptions());
		var logger = NullLogger<EventBatchMigrator>.Instance;

		Should.Throw<ArgumentNullException>(() => new EventBatchMigrator(null!, opts, logger));
		Should.Throw<ArgumentNullException>(() => new EventBatchMigrator(_eventStore, null!, logger));
		Should.Throw<ArgumentNullException>(() => new EventBatchMigrator(_eventStore, opts, null!));
	}

	private static List<StoredEvent> CreateStoredEvents(int count)
	{
		var events = new List<StoredEvent>();
		for (var i = 0; i < count; i++)
		{
			events.Add(new StoredEvent(
				$"evt-{i}", "agg-1", "TestAggregate", "TestEvent",
				Array.Empty<byte>(), null, i, DateTimeOffset.UtcNow, false));
		}

		return events;
	}

	private void SetupEventStoreLoad(string streamId, List<StoredEvent> events)
	{
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(streamId, streamId, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));
#pragma warning restore CA2012
	}

	private void SetupEventStoreAppendSuccess()
	{
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
			A<long>._, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(AppendResult.CreateSuccess(1, 0)));
#pragma warning restore CA2012
	}
}
