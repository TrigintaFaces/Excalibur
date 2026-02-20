// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="CounterSnapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class CounterSnapshotShould : UnitTestBase
{
	#region Property Tests

	[Fact]
	public void SetAndGetName()
	{
		// Arrange & Act
		var snapshot = new CounterSnapshot { Name = "requests_total" };

		// Assert
		snapshot.Name.ShouldBe("requests_total");
	}

	[Fact]
	public void SetAndGetValue()
	{
		// Arrange & Act
		var snapshot = new CounterSnapshot { Value = 12345 };

		// Assert
		snapshot.Value.ShouldBe(12345);
	}

	[Fact]
	public void SetAndGetUnit()
	{
		// Arrange & Act
		var snapshot = new CounterSnapshot { Unit = "bytes" };

		// Assert
		snapshot.Unit.ShouldBe("bytes");
	}

	[Fact]
	public void AllowNullUnit()
	{
		// Arrange & Act
		var snapshot = new CounterSnapshot { Unit = null };

		// Assert
		snapshot.Unit.ShouldBeNull();
	}

	[Fact]
	public void SetAndGetTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new CounterSnapshot { Timestamp = timestamp };

		// Assert
		snapshot.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenAllPropertiesMatch()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot
		{
			Name = "test",
			Value = 100,
			Unit = "requests",
			Timestamp = timestamp
		};
		var snapshot2 = new CounterSnapshot
		{
			Name = "test",
			Value = 100,
			Unit = "requests",
			Timestamp = timestamp
		};

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeTrue();
		(snapshot1 == snapshot2).ShouldBeTrue();
		(snapshot1 != snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenNameDiffers()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot { Name = "test1", Value = 100, Timestamp = timestamp };
		var snapshot2 = new CounterSnapshot { Name = "test2", Value = 100, Timestamp = timestamp };

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
		(snapshot1 != snapshot2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqualWhenValueDiffers()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = timestamp };
		var snapshot2 = new CounterSnapshot { Name = "test", Value = 200, Timestamp = timestamp };

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenUnitDiffers()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot { Name = "test", Value = 100, Unit = "bytes", Timestamp = timestamp };
		var snapshot2 = new CounterSnapshot { Name = "test", Value = 100, Unit = "requests", Timestamp = timestamp };

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenTimestampDiffers()
	{
		// Arrange
		var snapshot1 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = DateTimeOffset.UtcNow };
		var snapshot2 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = DateTimeOffset.UtcNow.AddSeconds(1) };

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForNull()
	{
		// Arrange
		var snapshot = new CounterSnapshot { Name = "test" };

		// Act & Assert
		snapshot.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForDifferentType()
	{
		// Arrange
		var snapshot = new CounterSnapshot { Name = "test" };

		// Act & Assert
		snapshot.Equals("not a snapshot").ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnTrueForMatchingSnapshot()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = timestamp };
		object snapshot2 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = timestamp };

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void ProduceConsistentHashCode()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot = new CounterSnapshot
		{
			Name = "test",
			Value = 100,
			Unit = "requests",
			Timestamp = timestamp
		};

		// Act
		var hash1 = snapshot.GetHashCode();
		var hash2 = snapshot.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ProduceSameHashCodeForEqualSnapshots()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var snapshot1 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = timestamp };
		var snapshot2 = new CounterSnapshot { Name = "test", Value = 100, Timestamp = timestamp };

		// Act & Assert
		snapshot1.GetHashCode().ShouldBe(snapshot2.GetHashCode());
	}

	#endregion
}
