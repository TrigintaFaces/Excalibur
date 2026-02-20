// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextSnapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextSnapshotShould : UnitTestBase
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "Handler",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
			FieldCount = 0,
			SizeBytes = 0,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		};

		// Assert
		snapshot.MessageId.ShouldBe("msg-123");
		snapshot.Stage.ShouldBe("Handler");
		snapshot.Fields.ShouldNotBeNull();
	}

	[Fact]
	public void StoreFieldsCorrectly()
	{
		// Arrange
		var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["CorrelationId"] = "corr-123",
			["TenantId"] = "tenant-456",
			["MessageType"] = "OrderCreated"
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "Middleware",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = fields,
			FieldCount = fields.Count,
			SizeBytes = 100,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		};

		// Assert
		snapshot.Fields.Count.ShouldBe(3);
		snapshot.Fields["CorrelationId"].ShouldBe("corr-123");
		snapshot.FieldCount.ShouldBe(3);
	}

	[Fact]
	public void StoreMetadataCorrectly()
	{
		// Arrange
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["source"] = "RabbitMQ",
			["priority"] = 1
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "Transport",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
			FieldCount = 0,
			SizeBytes = 50,
			Metadata = metadata
		};

		// Assert
		snapshot.Metadata.Count.ShouldBe(2);
		snapshot.Metadata["source"].ShouldBe("RabbitMQ");
	}

	[Fact]
	public void TrackSizeBytesAccurately()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "Handler",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
			FieldCount = 0,
			SizeBytes = 1024,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		};

		// Assert
		snapshot.SizeBytes.ShouldBe(1024);
	}

	[Fact]
	public void AllowNullFieldValues()
	{
		// Arrange
		var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["NullableField"] = null,
			["RealField"] = "value"
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "Handler",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = fields,
			FieldCount = 2,
			SizeBytes = 50,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		};

		// Assert
		snapshot.Fields["NullableField"].ShouldBeNull();
		snapshot.Fields["RealField"].ShouldBe("value");
	}
}
