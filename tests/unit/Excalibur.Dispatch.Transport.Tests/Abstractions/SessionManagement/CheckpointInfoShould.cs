// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="CheckpointInfo"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class CheckpointInfoShould
{
	[Fact]
	public void HaveEmptyCheckpointId_ByDefault()
	{
		// Arrange & Act
		var info = new CheckpointInfo();

		// Assert
		info.CheckpointId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptySessionId_ByDefault()
	{
		// Arrange & Act
		var info = new CheckpointInfo();

		// Assert
		info.SessionId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultCreatedAt_ByDefault()
	{
		// Arrange & Act
		var info = new CheckpointInfo();

		// Assert
		info.CreatedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveZeroSizeInBytes_ByDefault()
	{
		// Arrange & Act
		var info = new CheckpointInfo();

		// Assert
		info.SizeInBytes.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var info = new CheckpointInfo();

		// Assert
		info.Metadata.ShouldNotBeNull();
		info.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingCheckpointId()
	{
		// Arrange
		var info = new CheckpointInfo();

		// Act
		info.CheckpointId = "checkpoint-123";

		// Assert
		info.CheckpointId.ShouldBe("checkpoint-123");
	}

	[Fact]
	public void AllowSettingSessionId()
	{
		// Arrange
		var info = new CheckpointInfo();

		// Act
		info.SessionId = "session-456";

		// Assert
		info.SessionId.ShouldBe("session-456");
	}

	[Fact]
	public void AllowSettingCreatedAt()
	{
		// Arrange
		var info = new CheckpointInfo();
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		info.CreatedAt = createdAt;

		// Assert
		info.CreatedAt.ShouldBe(createdAt);
	}

	[Fact]
	public void AllowSettingSizeInBytes()
	{
		// Arrange
		var info = new CheckpointInfo();

		// Act
		info.SizeInBytes = 1024 * 1024; // 1 MB

		// Assert
		info.SizeInBytes.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var info = new CheckpointInfo();

		// Act
		info.Metadata["key1"] = "value1";
		info.Metadata["key2"] = "value2";

		// Assert
		info.Metadata.Count.ShouldBe(2);
		info.Metadata["key1"].ShouldBe("value1");
		info.Metadata["key2"].ShouldBe("value2");
	}

	[Fact]
	public void AllowLargeSizeInBytes()
	{
		// Arrange
		var info = new CheckpointInfo();

		// Act
		info.SizeInBytes = 10L * 1024 * 1024 * 1024; // 10 GB

		// Assert
		info.SizeInBytes.ShouldBe(10L * 1024 * 1024 * 1024);
	}

	[Fact]
	public void AllowCreatingFullyPopulatedCheckpointInfo()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var info = new CheckpointInfo
		{
			CheckpointId = "checkpoint-123",
			SessionId = "session-456",
			CreatedAt = createdAt,
			SizeInBytes = 5000,
		};
		info.Metadata["version"] = "1.0";
		info.Metadata["type"] = "incremental";

		// Assert
		info.CheckpointId.ShouldBe("checkpoint-123");
		info.SessionId.ShouldBe("session-456");
		info.CreatedAt.ShouldBe(createdAt);
		info.SizeInBytes.ShouldBe(5000);
		info.Metadata["version"].ShouldBe("1.0");
		info.Metadata["type"].ShouldBe("incremental");
	}
}
