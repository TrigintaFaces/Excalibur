// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="MessageBatch"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class MessageBatchShould
{
	[Fact]
	public void HaveNonEmptyBatchId_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.BatchId.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(batch.BatchId, out _).ShouldBeTrue();
	}

	[Fact]
	public void HaveUniqueeBatchIds()
	{
		// Arrange & Act
		var batch1 = new MessageBatch();
		var batch2 = new MessageBatch();

		// Assert
		batch1.BatchId.ShouldNotBe(batch2.BatchId);
	}

	[Fact]
	public void HaveEmptyMessages_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.Messages.ShouldNotBeNull();
		batch.Messages.ShouldBeEmpty();
	}

	[Fact]
	public void HaveCreatedAtSetToUtcNow_ByDefault()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var batch = new MessageBatch();

		// Assert
		var after = DateTimeOffset.UtcNow;
		batch.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		batch.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.Metadata.ShouldNotBeNull();
		batch.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void HaveZeroSize_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.Size.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroSizeInBytes_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.SizeInBytes.ShouldBe(0);
	}

	[Fact]
	public void HaveNullSource_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.Source.ShouldBeNull();
	}

	[Fact]
	public void HaveNormalPriority_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.Priority.ShouldBe(BatchPriority.Normal);
	}

	[Fact]
	public void HaveNullMaxProcessingTime_ByDefault()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.MaxProcessingTime.ShouldBeNull();
	}

	[Fact]
	public void IsEmpty_ReturnsTrueWhenNoMessages()
	{
		// Arrange & Act
		var batch = new MessageBatch();

		// Assert
		batch.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void IsEmpty_ReturnsFalseWhenHasMessages()
	{
		// Arrange
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { new() },
		};

		// Assert
		batch.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void Size_ReturnsMessageCount()
	{
		// Arrange
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { new(), new(), new() },
		};

		// Assert
		batch.Size.ShouldBe(3);
	}

	[Fact]
	public void SizeInBytes_ReturnsSumOfMessageBodyLengths()
	{
		// Arrange
		var message1 = new TransportMessage { Body = new ReadOnlyMemory<byte>(new byte[100]) };
		var message2 = new TransportMessage { Body = new ReadOnlyMemory<byte>(new byte[200]) };
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { message1, message2 },
		};

		// Assert
		batch.SizeInBytes.ShouldBe(300);
	}

	[Fact]
	public void SizeInBytes_ReturnsZeroForEmptyBodies()
	{
		// Arrange
		var message1 = new TransportMessage();
		var message2 = new TransportMessage();
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { message1, message2 },
		};

		// Assert
		batch.SizeInBytes.ShouldBe(0);
	}

	[Fact]
	public void IsFull_ReturnsTrueWhenAtMaxSize()
	{
		// Arrange
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { new(), new(), new() },
		};

		// Assert
		batch.IsFull(3).ShouldBeTrue();
	}

	[Fact]
	public void IsFull_ReturnsTrueWhenOverMaxSize()
	{
		// Arrange
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { new(), new(), new(), new() },
		};

		// Assert
		batch.IsFull(3).ShouldBeTrue();
	}

	[Fact]
	public void IsFull_ReturnsFalseWhenUnderMaxSize()
	{
		// Arrange
		var batch = new MessageBatch
		{
			Messages = new List<TransportMessage> { new(), new() },
		};

		// Assert
		batch.IsFull(3).ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBatchId()
	{
		// Arrange
		var batch = new MessageBatch();
		var customId = "custom-batch-id";

		// Act
		batch.BatchId = customId;

		// Assert
		batch.BatchId.ShouldBe(customId);
	}

	[Fact]
	public void AllowSettingMessages()
	{
		// Arrange
		var batch = new MessageBatch();
		var messages = new List<TransportMessage> { new(), new() };

		// Act
		batch.Messages = messages;

		// Assert
		batch.Messages.ShouldBe(messages);
		batch.Size.ShouldBe(2);
	}

	[Fact]
	public void AllowSettingCreatedAt()
	{
		// Arrange
		var batch = new MessageBatch();
		var customTime = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		batch.CreatedAt = customTime;

		// Assert
		batch.CreatedAt.ShouldBe(customTime);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var batch = new MessageBatch();

		// Act
		batch.Metadata["key1"] = "value1";
		batch.Metadata["key2"] = "value2";

		// Assert
		batch.Metadata.Count.ShouldBe(2);
		batch.Metadata["key1"].ShouldBe("value1");
		batch.Metadata["key2"].ShouldBe("value2");
	}

	[Fact]
	public void AllowSettingSource()
	{
		// Arrange
		var batch = new MessageBatch();

		// Act
		batch.Source = "my-queue";

		// Assert
		batch.Source.ShouldBe("my-queue");
	}

	[Fact]
	public void AllowSettingPriority()
	{
		// Arrange
		var batch = new MessageBatch();

		// Act
		batch.Priority = BatchPriority.High;

		// Assert
		batch.Priority.ShouldBe(BatchPriority.High);
	}

	[Fact]
	public void AllowSettingMaxProcessingTime()
	{
		// Arrange
		var batch = new MessageBatch();

		// Act
		batch.MaxProcessingTime = TimeSpan.FromMinutes(5);

		// Assert
		batch.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
	}
}
