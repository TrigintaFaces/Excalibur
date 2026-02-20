// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbStalePositionDetector"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify stale position detection logic.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbStalePositionDetectorShould
{
	#region StalePositionExceptionTypes Tests

	[Fact]
	public void StalePositionExceptionTypes_ContainsExpiredIteratorException()
	{
		// Assert
		DynamoDbStalePositionDetector.StalePositionExceptionTypes
			.ShouldContain("ExpiredIteratorException");
	}

	[Fact]
	public void StalePositionExceptionTypes_ContainsTrimmedDataAccessException()
	{
		// Assert
		DynamoDbStalePositionDetector.StalePositionExceptionTypes
			.ShouldContain("TrimmedDataAccessException");
	}

	[Fact]
	public void StalePositionExceptionTypes_ContainsResourceNotFoundException()
	{
		// Assert
		DynamoDbStalePositionDetector.StalePositionExceptionTypes
			.ShouldContain("ResourceNotFoundException");
	}

	[Fact]
	public void StalePositionExceptionTypes_HasThreeTypes()
	{
		// Assert
		DynamoDbStalePositionDetector.StalePositionExceptionTypes.Count.ShouldBe(3);
	}

	#endregion

	#region IsStalePositionException Tests

	[Fact]
	public void IsStalePositionException_ReturnsFalse_ForNull()
	{
		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(null);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForExpiredIteratorException()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForTrimmedDataAccessException()
	{
		// Arrange
		var exception = new TrimmedDataAccessException("Data trimmed");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForResourceNotFoundException()
	{
		// Arrange
		var exception = new ResourceNotFoundException("Resource not found");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForAggregateException_ContainingStaleException()
	{
		// Arrange
		var inner = new ExpiredIteratorException("Iterator expired");
		var exception = new AggregateException(inner);

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsFalse_ForUnrelatedAggregateException()
	{
		// Arrange
		var inner = new InvalidOperationException("Some error");
		var exception = new AggregateException(inner);

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForMessageContainingIteratorExpired()
	{
		// Arrange
		var exception = new AmazonDynamoDBException("The ITERATOR has EXPIRED");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForMessageContainingTrimHorizon()
	{
		// Arrange
		var exception = new AmazonDynamoDBException("Data beyond TRIM HORIZON");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForNestedStaleException()
	{
		// Arrange
		var innerMost = new ExpiredIteratorException("Iterator expired");
		var inner = new InvalidOperationException("Wrapper", innerMost);
		var exception = new InvalidOperationException("Outer", inner);

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsFalse_ForUnrelatedGenericException()
	{
		// Arrange
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = DynamoDbStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GetStalePositionExceptionType Tests

	[Fact]
	public void GetStalePositionExceptionType_ReturnsNull_ForNull()
	{
		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetStalePositionExceptionType_ReturnsExpiredIteratorException()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(exception);

		// Assert
		result.ShouldBe("ExpiredIteratorException");
	}

	[Fact]
	public void GetStalePositionExceptionType_ReturnsTrimmedDataAccessException()
	{
		// Arrange
		var exception = new TrimmedDataAccessException("Data trimmed");

		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(exception);

		// Assert
		result.ShouldBe("TrimmedDataAccessException");
	}

	[Fact]
	public void GetStalePositionExceptionType_ReturnsResourceNotFoundException()
	{
		// Arrange
		var exception = new ResourceNotFoundException("Resource not found");

		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(exception);

		// Assert
		result.ShouldBe("ResourceNotFoundException");
	}

	[Fact]
	public void GetStalePositionExceptionType_FindsTypeInAggregateException()
	{
		// Arrange
		var inner = new ExpiredIteratorException("Iterator expired");
		var exception = new AggregateException(inner);

		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(exception);

		// Assert
		result.ShouldBe("ExpiredIteratorException");
	}

	[Fact]
	public void GetStalePositionExceptionType_ReturnsNull_ForUnrelatedException()
	{
		// Arrange
		var exception = new InvalidOperationException("Some error");

		// Act
		var result = DynamoDbStalePositionDetector.GetStalePositionExceptionType(exception);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region GetReasonCode Tests

	[Fact]
	public void GetReasonCode_ReturnsUnknown_ForNull()
	{
		// Act
		var result = DynamoDbStalePositionDetector.GetReasonCode(null);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void GetReasonCode_ReturnsIteratorExpired_ForExpiredIteratorException()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.GetReasonCode(exception);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);
	}

	[Fact]
	public void GetReasonCode_ReturnsTrimmedData_ForTrimmedDataAccessException()
	{
		// Arrange
		var exception = new TrimmedDataAccessException("Data trimmed");

		// Act
		var result = DynamoDbStalePositionDetector.GetReasonCode(exception);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.TrimmedData);
	}

	[Fact]
	public void GetReasonCode_ReturnsShardNotFound_ForResourceNotFoundException()
	{
		// Arrange
		var exception = new ResourceNotFoundException("Resource not found");

		// Act
		var result = DynamoDbStalePositionDetector.GetReasonCode(exception);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.ShardNotFound);
	}

	[Fact]
	public void GetReasonCode_UsesMessageFallback_WhenNoKnownExceptionType()
	{
		// Arrange
		var exception = new InvalidOperationException("ITERATOR EXPIRED error");

		// Act
		var result = DynamoDbStalePositionDetector.GetReasonCode(exception);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);
	}

	#endregion

	#region CreateEventArgs Tests

	[Fact]
	public void CreateEventArgs_ThrowsArgumentNullException_WhenExceptionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DynamoDbStalePositionDetector.CreateEventArgs(null!, "processor-1"));
	}

	[Fact]
	public void CreateEventArgs_ThrowsArgumentException_WhenProcessorIdIsNull()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			DynamoDbStalePositionDetector.CreateEventArgs(exception, null!));
	}

	[Fact]
	public void CreateEventArgs_ThrowsArgumentException_WhenProcessorIdIsEmpty()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			DynamoDbStalePositionDetector.CreateEventArgs(exception, string.Empty));
	}

	[Fact]
	public void CreateEventArgs_SetsProcessorId()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "my-processor");

		// Assert
		result.ProcessorId.ShouldBe("my-processor");
	}

	[Fact]
	public void CreateEventArgs_SetsProviderType()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.ProviderType.ShouldBe("DynamoDB");
	}

	[Fact]
	public void CreateEventArgs_SetsReasonCode()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.ReasonCode.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);
	}

	[Fact]
	public void CreateEventArgs_SetsReasonMessage()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Custom message");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.ReasonMessage.ShouldBe("Custom message");
	}

	[Fact]
	public void CreateEventArgs_SetsOriginalException()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.OriginalException.ShouldBe(exception);
	}

	[Fact]
	public void CreateEventArgs_SetsDetectedAt()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.DetectedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.DetectedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void CreateEventArgs_IncludesStreamArnInContext()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			streamArn: "arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01");

		// Assert
		result.AdditionalContext.ShouldContainKey("StreamArn");
		result.AdditionalContext["StreamArn"].ShouldBe(
			"arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01");
	}

	[Fact]
	public void CreateEventArgs_IncludesTableNameInContext()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			tableName: "TestTable");

		// Assert
		result.AdditionalContext.ShouldContainKey("TableName");
		result.AdditionalContext["TableName"].ShouldBe("TestTable");
	}

	[Fact]
	public void CreateEventArgs_IncludesShardIdInContext()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			shardId: "shardId-00000001");

		// Assert
		result.AdditionalContext.ShouldContainKey("ShardId");
		result.AdditionalContext["ShardId"].ShouldBe("shardId-00000001");
	}

	[Fact]
	public void CreateEventArgs_IncludesSequenceNumberInContext()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			sequenceNumber: "123456789012345678901");

		// Assert
		result.AdditionalContext.ShouldContainKey("SequenceNumber");
		result.AdditionalContext["SequenceNumber"].ShouldBe("123456789012345678901");
	}

	[Fact]
	public void CreateEventArgs_IncludesExceptionTypeInContext()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.AdditionalContext.ShouldContainKey("ExceptionType");
		result.AdditionalContext["ExceptionType"].ShouldBe("ExpiredIteratorException");
	}

	[Fact]
	public void CreateEventArgs_SetsCaptureInstance_FromTableName()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			tableName: "TestTable");

		// Assert
		result.CaptureInstance.ShouldBe("TestTable");
	}

	[Fact]
	public void CreateEventArgs_SetsCaptureInstance_FromStreamArn_WhenTableNameIsNull()
	{
		// Arrange
		var exception = new ExpiredIteratorException("Iterator expired");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(
			exception,
			"processor-1",
			streamArn: "arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01");

		// Assert
		result.CaptureInstance.ShouldBe("arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2024-01-01");
	}

	[Fact]
	public void CreateEventArgs_AdditionalContextIsNull_WhenNoContextProvided()
	{
		// Arrange - use an exception without a known type so ExceptionType isn't added
		var exception = new InvalidOperationException("Some generic error");

		// Act
		var result = DynamoDbStalePositionDetector.CreateEventArgs(exception, "processor-1");

		// Assert
		result.AdditionalContext.ShouldBeNull();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbStalePositionDetector).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbStalePositionDetector).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbStalePositionDetector).IsPublic.ShouldBeTrue();
	}

	#endregion
}
