// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Streaming;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConsumeResultShould
{
	[Fact]
	public void HaveDefaultPropertyValues()
	{
		// Act
		var result = new ConsumeResult<string, string>();

		// Assert
		result.Key.ShouldBeNull();
		result.Value.ShouldBeNull();
		result.Topic.ShouldBe(string.Empty);
		result.Partition.ShouldBe(0);
		result.Offset.ShouldBe(0);
		result.Timestamp.ShouldBe(default);
		result.Headers.ShouldNotBeNull();
		result.Headers.Count.ShouldBe(0);
	}

	[Fact]
	public void SetAndGetAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var headers = new Dictionary<string, byte[]> { ["x-trace"] = new byte[] { 1, 2 } };

		// Act
		var result = new ConsumeResult<string, byte[]>
		{
			Key = "order-123",
			Value = new byte[] { 10, 20, 30 },
			Topic = "orders-topic",
			Partition = 4,
			Offset = 9876,
			Timestamp = timestamp,
			Headers = headers,
		};

		// Assert
		result.Key.ShouldBe("order-123");
		result.Value.ShouldBe(new byte[] { 10, 20, 30 });
		result.Topic.ShouldBe("orders-topic");
		result.Partition.ShouldBe(4);
		result.Offset.ShouldBe(9876);
		result.Timestamp.ShouldBe(timestamp);
		result.Headers.Count.ShouldBe(1);
		result.Headers["x-trace"].ShouldBe(new byte[] { 1, 2 });
	}

	[Fact]
	public void WorkWithIntKeyType()
	{
		// Act
		var result = new ConsumeResult<int, string>
		{
			Key = 42,
			Value = "payload",
		};

		// Assert
		result.Key.ShouldBe(42);
		result.Value.ShouldBe("payload");
	}

	[Fact]
	public void WorkWithNullableValueTypes()
	{
		// Act
		var result = new ConsumeResult<Guid, int>();

		// Assert
		result.Key.ShouldBe(Guid.Empty);
		result.Value.ShouldBe(0);
	}
}
