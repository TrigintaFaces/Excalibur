// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Migration;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

/// <summary>
/// Functional tests for <see cref="EventBatchMigrator"/> covering event migration
/// with filters, transforms, batching, dry-run, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventBatchMigratorFunctionalShould
{
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();

	private EventBatchMigrator CreateSut(MigrationOptions? opts = null)
	{
		var options = opts ?? new MigrationOptions { BatchSize = 100 };
		return new EventBatchMigrator(
			_eventStore,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<EventBatchMigrator>.Instance);
	}

	private static StoredEvent CreateStoredEvent(int version, string eventType = "TestEvent")
	{
		return new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: eventType,
			EventData: [],
			Metadata: null,
			Version: version,
			Timestamp: DateTimeOffset.UtcNow.AddMinutes(version),
			IsDispatched: false);
	}

	[Fact]
	public async Task MigrateAsync_ShouldProcessEventsFromSourceToTarget()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(0),
			CreateStoredEvent(1),
			CreateStoredEvent(2),
		};

		A.CallTo(() => _eventStore.LoadAsync("source", "source", A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(3, 0));

		var plan = new MigrationPlan("source", "target");
		var sut = CreateSut();

		// Act
		var result = await sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(3);
		result.EventsSkipped.ShouldBe(0);
		result.StreamsMigrated.ShouldBe(1);
	}

	[Fact]
	public async Task MigrateAsync_WithFilter_ShouldSkipFilteredEvents()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(0, "Keep"),
			CreateStoredEvent(1, "Skip"),
			CreateStoredEvent(2, "Keep"),
		};

		A.CallTo(() => _eventStore.LoadAsync("source", "source", A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(2, 0));

		var plan = new MigrationPlan("source", "target",
			EventFilter: e => e.EventType == "Keep");
		var sut = CreateSut();

		// Act
		var result = await sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
		result.EventsSkipped.ShouldBe(1);
	}

	[Fact]
	public async Task MigrateAsync_DryRun_ShouldNotWriteEvents()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(0),
			CreateStoredEvent(1),
		};

		A.CallTo(() => _eventStore.LoadAsync("source", "source", A<CancellationToken>._))
			.Returns(storedEvents);

		var plan = new MigrationPlan("source", "target");
		var sut = CreateSut(new MigrationOptions { DryRun = true, BatchSize = 100 });

		// Act
		var result = await sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
		result.IsDryRun.ShouldBeTrue();
		// AppendAsync should NOT have been called in dry-run mode
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MigrateAsync_WithMaxEvents_ShouldLimitMigration()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(0),
			CreateStoredEvent(1),
			CreateStoredEvent(2),
			CreateStoredEvent(3),
			CreateStoredEvent(4),
		};

		A.CallTo(() => _eventStore.LoadAsync("source", "source", A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(2, 0));

		var plan = new MigrationPlan("source", "target");
		var sut = CreateSut(new MigrationOptions { MaxEvents = 2, BatchSize = 100 });

		// Act
		var result = await sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(2);
	}

	[Fact]
	public async Task CreatePlanAsync_WithSourceStreamPattern_ShouldCreatePlan()
	{
		// Arrange
		var options = new MigrationOptions
		{
			SourceStreamPattern = "orders-stream",
			TargetStreamPrefix = "migrated-",
		};
		var sut = CreateSut();

		// Act
		var plans = await sut.CreatePlanAsync(options, CancellationToken.None);

		// Assert
		plans.Count.ShouldBe(1);
		plans[0].SourceStream.ShouldBe("orders-stream");
		plans[0].TargetStream.ShouldBe("migrated-orders-stream");
	}

	[Fact]
	public async Task CreatePlanAsync_WithoutSourceStreamPattern_ShouldReturnEmpty()
	{
		// Arrange
		var options = new MigrationOptions();
		var sut = CreateSut();

		// Act
		var plans = await sut.CreatePlanAsync(options, CancellationToken.None);

		// Assert
		plans.Count.ShouldBe(0);
	}

	[Fact]
	public async Task MigrateAsync_WithTransform_ShouldApplyTransformation()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			CreateStoredEvent(0, "OldEvent"),
		};

		A.CallTo(() => _eventStore.LoadAsync("source", "source", A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var plan = new MigrationPlan("source", "target",
			TransformFunc: e => new StoredEvent(
				e.EventId, e.AggregateId, e.AggregateType,
				"NewEvent", e.EventData, e.Metadata,
				e.Version, e.Timestamp, e.IsDispatched));

		var sut = CreateSut();

		// Act
		var result = await sut.MigrateAsync(plan, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(1);
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullEventStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventBatchMigrator(null!,
				Microsoft.Extensions.Options.Options.Create(new MigrationOptions()),
				NullLogger<EventBatchMigrator>.Instance));
	}

	[Fact]
	public async Task MigrateAsync_ShouldThrowOnNullPlan()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.MigrateAsync(null!, CancellationToken.None));
	}
}
