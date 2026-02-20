// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

/// <summary>
/// Functional tests for <see cref="MessageBatch"/> verifying
/// batch sizing, priority, metadata, and computed properties.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageBatchFunctionalShould
{
	[Fact]
	public void Default_batch_is_empty()
	{
		var batch = new MessageBatch();

		batch.IsEmpty.ShouldBeTrue();
		batch.Size.ShouldBe(0);
		batch.SizeInBytes.ShouldBe(0);
	}

	[Fact]
	public void Generate_batch_id_automatically()
	{
		var batch = new MessageBatch();

		batch.BatchId.ShouldNotBeNullOrWhiteSpace();
		// Should be a valid GUID
		Guid.TryParse(batch.BatchId, out _).ShouldBeTrue();
	}

	[Fact]
	public void Each_batch_gets_unique_id()
	{
		var batch1 = new MessageBatch();
		var batch2 = new MessageBatch();

		batch1.BatchId.ShouldNotBe(batch2.BatchId);
	}

	[Fact]
	public void Set_created_at_to_current_time()
	{
		var before = DateTimeOffset.UtcNow;
		var batch = new MessageBatch();
		var after = DateTimeOffset.UtcNow;

		batch.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		batch.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Calculate_size_from_messages()
	{
		var messages = new List<TransportMessage>
		{
			new() { Id = "1", Subject = "q", Body = new byte[] { 1 } },
			new() { Id = "2", Subject = "q", Body = new byte[] { 2, 3 } },
			new() { Id = "3", Subject = "q", Body = new byte[] { 4, 5, 6 } },
		};

		var batch = new MessageBatch { Messages = messages };

		batch.Size.ShouldBe(3);
		batch.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void Calculate_size_in_bytes_from_message_bodies()
	{
		var messages = new List<TransportMessage>
		{
			new() { Id = "1", Subject = "q", Body = new byte[100] },
			new() { Id = "2", Subject = "q", Body = new byte[200] },
			new() { Id = "3", Subject = "q", Body = new byte[50] },
		};

		var batch = new MessageBatch { Messages = messages };

		batch.SizeInBytes.ShouldBe(350);
	}

	[Fact]
	public void Handle_empty_bodies_in_size_calculation()
	{
		var messages = new List<TransportMessage>
		{
			new() { Id = "1", Subject = "q", Body = ReadOnlyMemory<byte>.Empty },
			new() { Id = "2", Subject = "q", Body = new byte[50] },
		};

		var batch = new MessageBatch { Messages = messages };

		batch.SizeInBytes.ShouldBe(50);
		batch.Size.ShouldBe(2);
	}

	[Fact]
	public void Report_full_when_at_max_size()
	{
		var messages = new List<TransportMessage>
		{
			new() { Id = "1", Subject = "q", Body = new byte[] { 1 } },
			new() { Id = "2", Subject = "q", Body = new byte[] { 2 } },
			new() { Id = "3", Subject = "q", Body = new byte[] { 3 } },
		};

		var batch = new MessageBatch { Messages = messages };

		batch.IsFull(3).ShouldBeTrue();
		batch.IsFull(5).ShouldBeFalse();
		batch.IsFull(2).ShouldBeTrue(); // Over capacity
	}

	[Fact]
	public void Default_priority_is_normal()
	{
		var batch = new MessageBatch();

		batch.Priority.ShouldBe(BatchPriority.Normal);
	}

	[Fact]
	public void Allow_setting_custom_priority()
	{
		var batch = new MessageBatch { Priority = BatchPriority.High };

		batch.Priority.ShouldBe(BatchPriority.High);
	}

	[Fact]
	public void Support_metadata()
	{
		var batch = new MessageBatch();

		batch.Metadata["region"] = "us-east-1";
		batch.Metadata["source"] = "payment-service";

		batch.Metadata.Count.ShouldBe(2);
		batch.Metadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void Support_source_property()
	{
		var batch = new MessageBatch { Source = "order-processor" };

		batch.Source.ShouldBe("order-processor");
	}

	[Fact]
	public void Support_max_processing_time()
	{
		var batch = new MessageBatch
		{
			MaxProcessingTime = TimeSpan.FromMinutes(5),
		};

		batch.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_max_processing_time_is_null()
	{
		var batch = new MessageBatch();

		batch.MaxProcessingTime.ShouldBeNull();
	}
}
