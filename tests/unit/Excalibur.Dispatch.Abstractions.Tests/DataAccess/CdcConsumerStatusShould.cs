// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.DataAccess;

/// <summary>
/// Unit tests for <see cref="CdcConsumerStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcConsumerStatusShould
{
	[Fact]
	public void HasCheckpoint_ReturnsFalse_WhenCurrentPositionIsNull()
	{
		// Arrange
		var status = new CdcConsumerStatus("consumer-1", null, null, null);

		// Act & Assert
		status.HasCheckpoint.ShouldBeFalse();
	}

	[Fact]
	public void HasCheckpoint_ReturnsTrue_WhenCurrentPositionIsNotNull()
	{
		// Arrange
		var position = new TokenChangePosition("token-1");
		var status = new CdcConsumerStatus("consumer-1", position, null, null);

		// Act & Assert
		status.HasCheckpoint.ShouldBeTrue();
	}

	[Fact]
	public void CanCalculateLag_ReturnsFalse_WhenCurrentPositionIsNull()
	{
		// Arrange
		var head = new TokenChangePosition("head-1");
		var status = new CdcConsumerStatus("consumer-1", null, head, null);

		// Act & Assert
		status.CanCalculateLag.ShouldBeFalse();
	}

	[Fact]
	public void CanCalculateLag_ReturnsFalse_WhenHeadPositionIsNull()
	{
		// Arrange
		var current = new TokenChangePosition("current-1");
		var status = new CdcConsumerStatus("consumer-1", current, null, null);

		// Act & Assert
		status.CanCalculateLag.ShouldBeFalse();
	}

	[Fact]
	public void CanCalculateLag_ReturnsTrue_WhenBothPositionsAreNotNull()
	{
		// Arrange
		var current = new TokenChangePosition("current-1");
		var head = new TokenChangePosition("head-1");
		var status = new CdcConsumerStatus("consumer-1", current, head, null);

		// Act & Assert
		status.CanCalculateLag.ShouldBeTrue();
	}

	[Fact]
	public void StoresConsumerId()
	{
		// Arrange & Act
		var status = new CdcConsumerStatus("my-consumer", null, null, null);

		// Assert
		status.ConsumerId.ShouldBe("my-consumer");
	}

	[Fact]
	public void StoresLastCheckpointTime()
	{
		// Arrange
		var checkpointTime = DateTimeOffset.UtcNow;
		var status = new CdcConsumerStatus("consumer-1", null, null, checkpointTime);

		// Assert
		status.LastCheckpointTime.ShouldBe(checkpointTime);
	}

	[Fact]
	public void SupportsRecordEquality()
	{
		// Arrange
		var position = new TokenChangePosition("pos-1");
		var time = DateTimeOffset.UtcNow;
		var status1 = new CdcConsumerStatus("consumer-1", position, null, time);
		var status2 = new CdcConsumerStatus("consumer-1", position, null, time);

		// Act & Assert
		status1.ShouldBe(status2);
	}

	[Fact]
	public void SupportsRecordInequality()
	{
		// Arrange
		var status1 = new CdcConsumerStatus("consumer-1", null, null, null);
		var status2 = new CdcConsumerStatus("consumer-2", null, null, null);

		// Act & Assert
		status1.ShouldNotBe(status2);
	}
}
