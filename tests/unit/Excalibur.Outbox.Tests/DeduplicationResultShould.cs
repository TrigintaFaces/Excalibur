// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeduplicationResultShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void DefaultConstructor_IsDuplicate_IsFalse()
	{
		// Arrange & Act
		var result = new DeduplicationResult();

		// Assert
		result.IsDuplicate.ShouldBeFalse();
	}

	[Fact]
	public void DefaultConstructor_FirstSeenAt_IsNull()
	{
		// Arrange & Act
		var result = new DeduplicationResult();

		// Assert
		result.FirstSeenAt.ShouldBeNull();
	}

	[Fact]
	public void DefaultConstructor_ProcessedBy_IsNull()
	{
		// Arrange & Act
		var result = new DeduplicationResult();

		// Assert
		result.ProcessedBy.ShouldBeNull();
	}

	#endregion

	#region Property Setting Tests

	[Fact]
	public void IsDuplicate_CanBeSetToTrue()
	{
		// Arrange
		var result = new DeduplicationResult();

		// Act
		result.IsDuplicate = true;

		// Assert
		result.IsDuplicate.ShouldBeTrue();
	}

	[Fact]
	public void FirstSeenAt_CanBeSet()
	{
		// Arrange
		var result = new DeduplicationResult();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		result.FirstSeenAt = timestamp;

		// Assert
		result.FirstSeenAt.ShouldBe(timestamp);
	}

	[Fact]
	public void ProcessedBy_CanBeSet()
	{
		// Arrange
		var result = new DeduplicationResult();

		// Act
		result.ProcessedBy = "processor-1";

		// Assert
		result.ProcessedBy.ShouldBe("processor-1");
	}

	#endregion

	#region Scenario Tests

	[Fact]
	public void DuplicateMessage_HasAllPropertiesSet()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

		// Act
		var result = new DeduplicationResult
		{
			IsDuplicate = true,
			FirstSeenAt = timestamp,
			ProcessedBy = "worker-1"
		};

		// Assert
		result.IsDuplicate.ShouldBeTrue();
		result.FirstSeenAt.ShouldBe(timestamp);
		result.ProcessedBy.ShouldBe("worker-1");
	}

	[Fact]
	public void NewMessage_HasMinimalProperties()
	{
		// Arrange & Act
		var result = new DeduplicationResult
		{
			IsDuplicate = false
		};

		// Assert
		result.IsDuplicate.ShouldBeFalse();
		result.FirstSeenAt.ShouldBeNull();
		result.ProcessedBy.ShouldBeNull();
	}

	#endregion
}
