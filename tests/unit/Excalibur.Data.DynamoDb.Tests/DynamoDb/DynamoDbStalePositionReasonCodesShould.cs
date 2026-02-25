// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbStalePositionReasonCodes"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify reason code constants and detection methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbStalePositionReasonCodesShould
{
	#region Constant Value Tests

	[Fact]
	public void IteratorExpired_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.IteratorExpired.ShouldBe("DYNAMODB_ITERATOR_EXPIRED");
	}

	[Fact]
	public void TrimmedData_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.TrimmedData.ShouldBe("DYNAMODB_TRIMMED_DATA");
	}

	[Fact]
	public void ShardClosed_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.ShardClosed.ShouldBe("DYNAMODB_SHARD_CLOSED");
	}

	[Fact]
	public void ShardNotFound_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.ShardNotFound.ShouldBe("DYNAMODB_SHARD_NOT_FOUND");
	}

	[Fact]
	public void StreamNotFound_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.StreamNotFound.ShouldBe("DYNAMODB_STREAM_NOT_FOUND");
	}

	[Fact]
	public void StreamDisabled_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.StreamDisabled.ShouldBe("DYNAMODB_STREAM_DISABLED");
	}

	[Fact]
	public void Unknown_HasCorrectValue()
	{
		// Assert
		DynamoDbStalePositionReasonCodes.Unknown.ShouldBe("DYNAMODB_UNKNOWN");
	}

	#endregion

	#region FromExceptionType Tests

	[Theory]
	[InlineData("ExpiredIteratorException", "DYNAMODB_ITERATOR_EXPIRED")]
	[InlineData("TrimmedDataAccessException", "DYNAMODB_TRIMMED_DATA")]
	[InlineData("ResourceNotFoundException", "DYNAMODB_SHARD_NOT_FOUND")]
	public void FromExceptionType_ReturnsCorrectCode_ForKnownExceptions(string exceptionType, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("SomeExpiredException", "DYNAMODB_ITERATOR_EXPIRED")]
	[InlineData("IteratorExpiredException", "DYNAMODB_ITERATOR_EXPIRED")]
	public void FromExceptionType_DetectsExpired_InTypeName(string exceptionType, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("DataTrimmedException", "DYNAMODB_TRIMMED_DATA")]
	[InlineData("TrimmedAccessException", "DYNAMODB_TRIMMED_DATA")]
	public void FromExceptionType_DetectsTrimmed_InTypeName(string exceptionType, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("ShardNotFoundException", "DYNAMODB_SHARD_NOT_FOUND")]
	[InlineData("StreamNotFoundException", "DYNAMODB_SHARD_NOT_FOUND")]
	public void FromExceptionType_DetectsNotFound_InTypeName(string exceptionType, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FromExceptionType_ReturnsUnknown_ForNullOrWhitespace(string? exceptionType)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromExceptionType_ReturnsUnknown_ForUnrecognizedExceptionType()
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromExceptionType("SomeOtherException");

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	#endregion

	#region FromErrorMessage Tests

	[Theory]
	[InlineData("Iterator has expired", "DYNAMODB_ITERATOR_EXPIRED")]
	[InlineData("ITERATOR INVALID", "DYNAMODB_ITERATOR_EXPIRED")]
	[InlineData("The iterator has expired after 15 minutes", "DYNAMODB_ITERATOR_EXPIRED")]
	public void FromErrorMessage_DetectsIteratorExpired(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("Data beyond trim horizon", "DYNAMODB_TRIMMED_DATA")]
	[InlineData("TRIM HORIZON exceeded", "DYNAMODB_TRIMMED_DATA")]
	[InlineData("Data has been trimmed", "DYNAMODB_TRIMMED_DATA")]
	[InlineData("Sequence out of range", "DYNAMODB_TRIMMED_DATA")]
	public void FromErrorMessage_DetectsTrimmedData(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("Shard closed", "DYNAMODB_SHARD_CLOSED")]
	[InlineData("SHARD has been CLOSED", "DYNAMODB_SHARD_CLOSED")]
	public void FromErrorMessage_DetectsShardClosed(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("Shard not found", "DYNAMODB_SHARD_NOT_FOUND")]
	[InlineData("SHARD NOT FOUND in stream", "DYNAMODB_SHARD_NOT_FOUND")]
	public void FromErrorMessage_DetectsShardNotFound(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("Stream disabled", "DYNAMODB_STREAM_DISABLED")]
	[InlineData("STREAM has been DISABLED", "DYNAMODB_STREAM_DISABLED")]
	public void FromErrorMessage_DetectsStreamDisabled(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData("Stream not found", "DYNAMODB_STREAM_NOT_FOUND")]
	[InlineData("STREAM does not exist", "DYNAMODB_STREAM_NOT_FOUND")]
	public void FromErrorMessage_DetectsStreamNotFound(string message, string expectedCode)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(expectedCode);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FromErrorMessage_ReturnsUnknown_ForNullOrWhitespace(string? message)
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_ReturnsUnknown_ForUnrecognizedMessage()
	{
		// Act
		var result = DynamoDbStalePositionReasonCodes.FromErrorMessage("Some random error occurred");

		// Assert
		result.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbStalePositionReasonCodes).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbStalePositionReasonCodes).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbStalePositionReasonCodes).IsPublic.ShouldBeTrue();
	}

	#endregion
}
