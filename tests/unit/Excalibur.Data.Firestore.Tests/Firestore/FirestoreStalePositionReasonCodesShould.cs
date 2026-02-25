// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreStalePositionReasonCodes"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify reason code constants and conversion methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreStalePositionReasonCodesShould
{
	#region Constant Value Tests

	[Fact]
	public void HaveDeadlineExceededConstant()
	{
		FirestoreStalePositionReasonCodes.DeadlineExceeded.ShouldBe("FIRESTORE_DEADLINE_EXCEEDED");
	}

	[Fact]
	public void HaveNotFoundConstant()
	{
		FirestoreStalePositionReasonCodes.NotFound.ShouldBe("FIRESTORE_NOT_FOUND");
	}

	[Fact]
	public void HavePermissionDeniedConstant()
	{
		FirestoreStalePositionReasonCodes.PermissionDenied.ShouldBe("FIRESTORE_PERMISSION_DENIED");
	}

	[Fact]
	public void HaveUnavailableConstant()
	{
		FirestoreStalePositionReasonCodes.Unavailable.ShouldBe("FIRESTORE_UNAVAILABLE");
	}

	[Fact]
	public void HaveCancelledConstant()
	{
		FirestoreStalePositionReasonCodes.Cancelled.ShouldBe("FIRESTORE_CANCELLED");
	}

	[Fact]
	public void HaveResourceExhaustedConstant()
	{
		FirestoreStalePositionReasonCodes.ResourceExhausted.ShouldBe("FIRESTORE_RESOURCE_EXHAUSTED");
	}

	[Fact]
	public void HaveAbortedConstant()
	{
		FirestoreStalePositionReasonCodes.Aborted.ShouldBe("FIRESTORE_ABORTED");
	}

	[Fact]
	public void HaveInternalConstant()
	{
		FirestoreStalePositionReasonCodes.Internal.ShouldBe("FIRESTORE_INTERNAL");
	}

	[Fact]
	public void HaveUnknownConstant()
	{
		FirestoreStalePositionReasonCodes.Unknown.ShouldBe("FIRESTORE_UNKNOWN");
	}

	#endregion

	#region FromGrpcStatusCode Tests

	[Theory]
	[InlineData(1, "FIRESTORE_CANCELLED")]
	[InlineData(4, "FIRESTORE_DEADLINE_EXCEEDED")]
	[InlineData(5, "FIRESTORE_NOT_FOUND")]
	[InlineData(7, "FIRESTORE_PERMISSION_DENIED")]
	[InlineData(8, "FIRESTORE_RESOURCE_EXHAUSTED")]
	[InlineData(10, "FIRESTORE_ABORTED")]
	[InlineData(13, "FIRESTORE_INTERNAL")]
	[InlineData(14, "FIRESTORE_UNAVAILABLE")]
	public void FromGrpcStatusCode_ReturnsCorrectCode(int statusCode, string expected)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromGrpcStatusCode(statusCode);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(6)]
	[InlineData(9)]
	[InlineData(11)]
	[InlineData(12)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(-1)]
	[InlineData(100)]
	public void FromGrpcStatusCode_ReturnsUnknown_ForUnmappedCodes(int statusCode)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromGrpcStatusCode(statusCode);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	#endregion

	#region FromErrorMessage Tests

	[Fact]
	public void FromErrorMessage_ReturnsUnknown_WhenMessageIsNull()
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(null);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_ReturnsUnknown_WhenMessageIsEmpty()
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage("");

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_ReturnsUnknown_WhenMessageIsWhitespace()
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage("   ");

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	[Theory]
	[InlineData("DEADLINE exceeded")]
	[InlineData("Operation deadline exceeded")]
	[InlineData("Request DEADLINE_EXCEEDED")]
	public void FromErrorMessage_DetectsDeadlineExceeded(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.DeadlineExceeded);
	}

	[Theory]
	[InlineData("Request timeout occurred")]
	[InlineData("Operation TIMEOUT")]
	public void FromErrorMessage_DetectsTimeoutAsDeadlineExceeded(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.DeadlineExceeded);
	}

	[Theory]
	[InlineData("Collection not found")]
	[InlineData("COLLECTION NOT FOUND")]
	[InlineData("Document not found")]
	[InlineData("Path notfound error")]
	public void FromErrorMessage_DetectsNotFound(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.NotFound);
	}

	[Theory]
	[InlineData("Permission denied")]
	[InlineData("PERMISSION_DENIED")]
	[InlineData("Access denied")]
	[InlineData("DENIED access")]
	[InlineData("Unauthorized access")]
	public void FromErrorMessage_DetectsPermissionDenied(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.PermissionDenied);
	}

	[Theory]
	[InlineData("Service unavailable")]
	[InlineData("UNAVAILABLE")]
	[InlineData("Service is down")]
	public void FromErrorMessage_DetectsUnavailable(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unavailable);
	}

	[Theory]
	[InlineData("Request cancelled")]
	[InlineData("CANCELLED")]
	[InlineData("Operation canceled")]
	[InlineData("CANCELED by user")]
	public void FromErrorMessage_DetectsCancelled(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Cancelled);
	}

	[Theory]
	[InlineData("Quota exceeded")]
	[InlineData("QUOTA limit reached")]
	[InlineData("Resource exhausted")]
	[InlineData("EXHAUSTED")]
	[InlineData("Rate limit exceeded")]
	public void FromErrorMessage_DetectsResourceExhausted(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.ResourceExhausted);
	}

	[Theory]
	[InlineData("Request aborted")]
	[InlineData("ABORTED")]
	[InlineData("Conflict detected")]
	[InlineData("CONFLICT with another operation")]
	public void FromErrorMessage_DetectsAborted(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Aborted);
	}

	[Theory]
	[InlineData("Internal error")]
	[InlineData("INTERNAL server error")]
	public void FromErrorMessage_DetectsInternal(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Internal);
	}

	[Theory]
	[InlineData("Unknown error occurred")]
	[InlineData("Something went wrong")]
	[InlineData("Generic failure")]
	public void FromErrorMessage_ReturnsUnknown_ForUnmappedMessages(string message)
	{
		// Act
		var result = FirestoreStalePositionReasonCodes.FromErrorMessage(message);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeStatic()
	{
		// Assert
		typeof(FirestoreStalePositionReasonCodes).IsAbstract.ShouldBeTrue();
		typeof(FirestoreStalePositionReasonCodes).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreStalePositionReasonCodes).IsPublic.ShouldBeTrue();
	}

	#endregion
}
