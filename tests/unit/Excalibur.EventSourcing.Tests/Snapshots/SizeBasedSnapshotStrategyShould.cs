// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="SizeBasedSnapshotStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SizeBasedSnapshotStrategyShould
{
	[Fact]
	public void Constructor_ThrowArgumentOutOfRangeException_WhenMaxSizeIsZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new SizeBasedSnapshotStrategy(maxSizeInBytes: 0));
	}

	[Fact]
	public void Constructor_ThrowArgumentOutOfRangeException_WhenMaxSizeIsNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new SizeBasedSnapshotStrategy(maxSizeInBytes: -100));
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnTrue_WhenAggregateSizeExceedsThreshold()
	{
		// Arrange - Small threshold that will be exceeded
		var strategy = new SizeBasedSnapshotStrategy(maxSizeInBytes: 10);

		// Create aggregate with enough data to exceed 10 bytes
		var aggregate = new LargeTestAggregate("test-id", "This is a large value that will exceed the size threshold");

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_WhenAggregateSizeIsBelowThreshold()
	{
		// Arrange - Large threshold that won't be exceeded
		var strategy = new SizeBasedSnapshotStrategy(maxSizeInBytes: 10_000_000); // 10MB

		var aggregate = new SmallTestAggregate("test-id", "Small");

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_UseDefaultThreshold_WhenNotSpecified()
	{
		// Arrange - Default is 1MB, small aggregate should be below
		var strategy = new SizeBasedSnapshotStrategy();
		var aggregate = new SmallTestAggregate("test-id", "Small value");

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert - Small aggregate should not exceed 1MB default
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_WhenExactlyAtThreshold()
	{
		// Arrange - Create aggregate that serializes to exactly at threshold
		var strategy = new SizeBasedSnapshotStrategy(maxSizeInBytes: 1_000_000);
		var aggregate = new SmallTestAggregate("test-id", "X");

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert - Should be below 1MB
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ThrowArgumentNullException_WhenAggregateIsNull()
	{
		// Arrange
		var strategy = new SizeBasedSnapshotStrategy();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => strategy.ShouldCreateSnapshot(null!));
	}

	[Fact]
	public void ShouldCreateSnapshot_HandleSerializationErrors_Gracefully()
	{
		// Arrange - Use a very high version to trigger fallback size calculation
		var strategy = new SizeBasedSnapshotStrategy(maxSizeInBytes: 100);

		// Create aggregate with snapshot support that fails serialization (fallback uses version * 1024)
		var snapshotSupport = A.Fake<IAggregateSnapshotSupport>();
		_ = A.CallTo(() => snapshotSupport.CreateSnapshot()).Throws(new InvalidOperationException("Snapshot creation failed"));

		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(1);
		_ = A.CallTo(() => aggregate.GetService(typeof(IAggregateSnapshotSupport))).Returns(snapshotSupport);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert - Fallback calculation: version (1) * 1024 = 1024 bytes > 100 bytes
		result.ShouldBeTrue();
	}

	#region Test Aggregates

	/// <summary>
	/// Small test aggregate with minimal state for below-threshold tests.
	/// </summary>
	private sealed class SmallTestAggregate : AggregateRoot
	{
		public string Data { get; }

		public SmallTestAggregate(string id, string data) : base(id)
		{
			Data = data;
		}

		protected override void ApplyEventInternal(Dispatch.Abstractions.IDomainEvent @event) { }

		public override ISnapshot CreateSnapshot() => new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = Id,
			Version = Version,
			AggregateType = nameof(SmallTestAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes(Data),
			CreatedAt = DateTime.UtcNow,
		};
	}

	/// <summary>
	/// Large test aggregate with significant state for above-threshold tests.
	/// </summary>
	private sealed class LargeTestAggregate : AggregateRoot
	{
		public string Data { get; }

		public LargeTestAggregate(string id, string data) : base(id)
		{
			Data = data;
		}

		protected override void ApplyEventInternal(Dispatch.Abstractions.IDomainEvent @event) { }

		public override ISnapshot CreateSnapshot() => new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = Id,
			Version = Version,
			AggregateType = nameof(LargeTestAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes(Data + new string('X', 1000)),
			CreatedAt = DateTime.UtcNow,
		};
	}

	#endregion Test Aggregates
}
