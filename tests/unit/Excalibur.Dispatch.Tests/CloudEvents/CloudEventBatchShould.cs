// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventBatchShould
{
	private static CloudEvent CreateTestEvent(string? data = null) =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("urn:test"),
			Type = "test.type",
			Time = DateTimeOffset.UtcNow,
			Data = data ?? "test-data",
		};

	// --- Constructor and defaults ---

	[Fact]
	public void DefaultConstructor_CreatesEmptyBatch()
	{
		// Act
		var batch = new CloudEventBatch();

		// Assert
		batch.Count.ShouldBe(0);
		batch.CurrentBatchSize.ShouldBe(0);
		batch.MaxEvents.ShouldBe(100);
		batch.MaxBatchSize.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void OptionsConstructor_AppliesOptions()
	{
		// Arrange
		var options = new CloudEventBatchOptions
		{
			MaxEvents = 5,
			MaxBatchSizeBytes = 2048,
			InitialCapacity = 3,
		};

		// Act
		var batch = new CloudEventBatch(options);

		// Assert
		batch.MaxEvents.ShouldBe(5);
		batch.MaxBatchSize.ShouldBe(2048);
		batch.Count.ShouldBe(0);
	}

	[Fact]
	public void EventsConstructor_AddsEvents()
	{
		// Arrange
		var events = new[] { CreateTestEvent(), CreateTestEvent() };

		// Act
		var batch = new CloudEventBatch(events);

		// Assert
		batch.Count.ShouldBe(2);
	}

	[Fact]
	public void EventsConstructor_WithNullEvents_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CloudEventBatch((IEnumerable<CloudEvent>)null!));
	}

	// --- TryAdd ---

	[Fact]
	public void TryAdd_WithinLimits_ReturnsTrue()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act
		var result = batch.TryAdd(CreateTestEvent());

		// Assert
		result.ShouldBeTrue();
		batch.Count.ShouldBe(1);
		batch.CurrentBatchSize.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void TryAdd_WithNullEvent_Throws()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => batch.TryAdd(null!));
	}

	[Fact]
	public void TryAdd_ExceedsMaxEvents_ReturnsFalse()
	{
		// Arrange
		var options = new CloudEventBatchOptions { MaxEvents = 2, MaxBatchSizeBytes = long.MaxValue };
		var batch = new CloudEventBatch(options);
		batch.TryAdd(CreateTestEvent());
		batch.TryAdd(CreateTestEvent());

		// Act
		var result = batch.TryAdd(CreateTestEvent());

		// Assert
		result.ShouldBeFalse();
		batch.Count.ShouldBe(2);
	}

	// --- Indexer ---

	[Fact]
	public void Indexer_ReturnsCorrectEvent()
	{
		// Arrange
		var evt1 = CreateTestEvent("data1");
		var evt2 = CreateTestEvent("data2");
		var batch = new CloudEventBatch();
		batch.TryAdd(evt1);
		batch.TryAdd(evt2);

		// Assert
		batch[0].ShouldBe(evt1);
		batch[1].ShouldBe(evt2);
	}

	// --- AddRange ---

	[Fact]
	public void AddRange_AddsMultipleEvents()
	{
		// Arrange
		var batch = new CloudEventBatch();
		var events = new[] { CreateTestEvent(), CreateTestEvent(), CreateTestEvent() };

		// Act
		var added = batch.AddRange(events);

		// Assert
		added.ShouldBe(3);
		batch.Count.ShouldBe(3);
	}

	[Fact]
	public void AddRange_StopsWhenLimitReached()
	{
		// Arrange
		var options = new CloudEventBatchOptions { MaxEvents = 2, MaxBatchSizeBytes = long.MaxValue };
		var batch = new CloudEventBatch(options);
		var events = new[] { CreateTestEvent(), CreateTestEvent(), CreateTestEvent() };

		// Act
		var added = batch.AddRange(events);

		// Assert
		added.ShouldBe(2);
		batch.Count.ShouldBe(2);
	}

	[Fact]
	public void AddRange_WithNullEvents_Throws()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => batch.AddRange(null!));
	}

	// --- Clear ---

	[Fact]
	public void Clear_RemovesAllEvents()
	{
		// Arrange
		var batch = new CloudEventBatch();
		batch.TryAdd(CreateTestEvent());
		batch.TryAdd(CreateTestEvent());

		// Act
		batch.Clear();

		// Assert
		batch.Count.ShouldBe(0);
		batch.CurrentBatchSize.ShouldBe(0);
	}

	// --- GetEnumerator ---

	[Fact]
	public void GetEnumerator_IteratesAllEvents()
	{
		// Arrange
		var batch = new CloudEventBatch();
		batch.TryAdd(CreateTestEvent());
		batch.TryAdd(CreateTestEvent());

		// Act
		var count = 0;
		foreach (var evt in batch)
		{
			count++;
			evt.ShouldNotBeNull();
		}

		// Assert
		count.ShouldBe(2);
	}

	// --- Split ---

	[Fact]
	public void Split_WhenWithinLimits_ReturnsSelf()
	{
		// Arrange
		var batch = new CloudEventBatch();
		batch.TryAdd(CreateTestEvent());

		// Act
		var result = batch.Split();

		// Assert
		result.Count.ShouldBe(1);
		result[0].ShouldBe(batch);
	}

	// --- SchemaCompatibilityMode ---

	[Fact]
	public void SchemaCompatibilityMode_HasExpectedValues()
	{
		// Assert
		((int)SchemaCompatibilityMode.None).ShouldBe(0);
		((int)SchemaCompatibilityMode.Forward).ShouldBe(1);
		((int)SchemaCompatibilityMode.Backward).ShouldBe(2);
		((int)SchemaCompatibilityMode.Full).ShouldBe(3);
	}

	[Fact]
	public void CloudEventMode_HasExpectedValues()
	{
		// Assert
		((int)CloudEventMode.Structured).ShouldBe(0);
		((int)CloudEventMode.Binary).ShouldBe(1);
	}
}
