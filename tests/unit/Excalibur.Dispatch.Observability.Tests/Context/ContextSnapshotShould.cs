// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextSnapshot"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextSnapshotShould
{
	#region Required Property Tests

	[Fact]
	public void RequireMessageId()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-123",
			Stage = "PreHandler",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void RequireStage()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-456",
			Stage = "PostHandler",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Stage.ShouldBe("PostHandler");
	}

	[Fact]
	public void RequireFields()
	{
		// Arrange
		var fields = new Dictionary<string, object?>
		{
			["CorrelationId"] = "corr-123",
			["UserId"] = "user-456",
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-789",
			Stage = "Handler",
			Fields = fields,
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Fields.ShouldBe(fields);
		snapshot.Fields.Count.ShouldBe(2);
	}

	[Fact]
	public void RequireMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			["Version"] = "1.0",
			["Source"] = "Test",
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-abc",
			Stage = "Middleware",
			Fields = new Dictionary<string, object?>(),
			Metadata = metadata,
		};

		// Assert
		snapshot.Metadata.ShouldBe(metadata);
		snapshot.Metadata.Count.ShouldBe(2);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-def",
			Stage = "Test",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultFieldCount()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-ghi",
			Stage = "Test",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.FieldCount.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultSizeBytes()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-jkl",
			Stage = "Test",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.SizeBytes.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-mno",
			Stage = "Test",
			Timestamp = timestamp,
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowSettingFieldCount()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-pqr",
			Stage = "Test",
			FieldCount = 10,
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.FieldCount.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingSizeBytes()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-stu",
			Stage = "Test",
			SizeBytes = 1024,
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.SizeBytes.ShouldBe(1024);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var fields = new Dictionary<string, object?>
		{
			["CorrelationId"] = "corr-xyz",
			["RequestId"] = "req-123",
			["UserId"] = "user-456",
		};
		var metadata = new Dictionary<string, object>
		{
			["CapturedBy"] = "ContextObservabilityMiddleware",
			["Version"] = 1,
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-vwx",
			Stage = "PostHandler",
			Timestamp = timestamp,
			Fields = fields,
			FieldCount = 3,
			SizeBytes = 256,
			Metadata = metadata,
		};

		// Assert
		snapshot.MessageId.ShouldBe("msg-vwx");
		snapshot.Stage.ShouldBe("PostHandler");
		snapshot.Timestamp.ShouldBe(timestamp);
		snapshot.Fields.Count.ShouldBe(3);
		snapshot.FieldCount.ShouldBe(3);
		snapshot.SizeBytes.ShouldBe(256);
		snapshot.Metadata.Count.ShouldBe(2);
	}

	[Fact]
	public void SupportNullFieldValues()
	{
		// Arrange
		var fields = new Dictionary<string, object?>
		{
			["NullableField"] = null,
			["NonNullField"] = "value",
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-null",
			Stage = "Test",
			Fields = fields,
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Fields["NullableField"].ShouldBeNull();
		snapshot.Fields["NonNullField"].ShouldBe("value");
	}

	[Fact]
	public void SupportEmptyCollections()
	{
		// Arrange & Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-empty",
			Stage = "Empty",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Fields.ShouldBeEmpty();
		snapshot.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void SupportDifferentFieldValueTypes()
	{
		// Arrange
		var fields = new Dictionary<string, object?>
		{
			["StringValue"] = "text",
			["IntValue"] = 42,
			["BoolValue"] = true,
			["DoubleValue"] = 3.14,
			["DateValue"] = DateTimeOffset.UtcNow,
		};

		// Act
		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-types",
			Stage = "Types",
			Fields = fields,
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		snapshot.Fields["StringValue"].ShouldBeOfType<string>();
		snapshot.Fields["IntValue"].ShouldBeOfType<int>();
		snapshot.Fields["BoolValue"].ShouldBeOfType<bool>();
		snapshot.Fields["DoubleValue"].ShouldBeOfType<double>();
		snapshot.Fields["DateValue"].ShouldBeOfType<DateTimeOffset>();
	}

	#endregion
}
