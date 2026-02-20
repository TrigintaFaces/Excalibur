// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbStalePositionException"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify exception constructors and properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbStalePositionExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_SetsDefaultMessage()
	{
		// Act
		var exception = new DynamoDbStalePositionException();

		// Assert
		exception.Message.ShouldContain("stale");
		exception.InnerException.ShouldBeNull();
		exception.EventArgs.ShouldBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Custom error message";

		// Act
		var exception = new DynamoDbStalePositionException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Custom error message";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new DynamoDbStalePositionException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void EventArgsConstructor_SetsEventArgs()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ReasonCode = "EXPIRED_ITERATOR",
			StalePosition = new byte[] { 1, 2, 3 },
			DetectedAt = DateTimeOffset.UtcNow
		};

		// Act
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.EventArgs.ShouldBe(eventArgs);
		exception.ProcessorId.ShouldBe("test-processor");
		exception.ReasonCode.ShouldBe("EXPIRED_ITERATOR");
	}

	[Fact]
	public void MessageAndEventArgsConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Custom error message";
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ReasonCode = "DATA_TRIMMED"
		};

		// Act
		var exception = new DynamoDbStalePositionException(message, eventArgs);

		// Assert
		exception.Message.ShouldBe(message);
		exception.EventArgs.ShouldBe(eventArgs);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ProcessorId_ReturnsNull_WhenEventArgsIsNull()
	{
		// Arrange
		var exception = new DynamoDbStalePositionException();

		// Assert
		exception.ProcessorId.ShouldBeNull();
	}

	[Fact]
	public void ReasonCode_ReturnsNull_WhenEventArgsIsNull()
	{
		// Arrange
		var exception = new DynamoDbStalePositionException();

		// Assert
		exception.ReasonCode.ShouldBeNull();
	}

	[Fact]
	public void StalePosition_ReturnsNull_WhenEventArgsIsNull()
	{
		// Arrange
		var exception = new DynamoDbStalePositionException();

		// Assert
		exception.StalePosition.ShouldBeNull();
	}

	[Fact]
	public void StreamArn_ReturnsValue_FromAdditionalContext()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>
			{
				["StreamArn"] = "arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01T00:00:00.000"
			}
		};
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.StreamArn.ShouldBe("arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01T00:00:00.000");
	}

	[Fact]
	public void TableName_ReturnsCaptureInstance_WhenSet()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			CaptureInstance = "TestTable"
		};
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.TableName.ShouldBe("TestTable");
	}

	[Fact]
	public void TableName_ReturnsFromAdditionalContext_WhenCaptureInstanceNotSet()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>
			{
				["TableName"] = "TestTable"
			}
		};
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.TableName.ShouldBe("TestTable");
	}

	[Fact]
	public void ShardId_ReturnsValue_FromAdditionalContext()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>
			{
				["ShardId"] = "shardId-00000001"
			}
		};
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.ShardId.ShouldBe("shardId-00000001");
	}

	[Fact]
	public void SequenceNumber_ReturnsValue_FromAdditionalContext()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>
			{
				["SequenceNumber"] = "123456789012345678901"
			}
		};
		var exception = new DynamoDbStalePositionException(eventArgs);

		// Assert
		exception.SequenceNumber.ShouldBe("123456789012345678901");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbStalePositionException).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbStalePositionException).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void InheritsFromException()
	{
		// Assert
		typeof(Exception).IsAssignableFrom(typeof(DynamoDbStalePositionException)).ShouldBeTrue();
	}

	#endregion
}
