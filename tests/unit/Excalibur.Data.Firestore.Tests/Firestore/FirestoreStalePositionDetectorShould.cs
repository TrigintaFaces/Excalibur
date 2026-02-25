// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

using Grpc.Core;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreStalePositionDetector"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify stale position detection from exceptions and status codes.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreStalePositionDetectorShould
{
	private const string TestProcessorId = "test-processor";
	private const string TestCollectionPath = "test-collection";

	#region gRPC Status Code Constants

	[Fact]
	public void HaveCorrectGrpcCancelledConstant()
	{
		FirestoreStalePositionDetector.GrpcCancelled.ShouldBe(1);
	}

	[Fact]
	public void HaveCorrectGrpcDeadlineExceededConstant()
	{
		FirestoreStalePositionDetector.GrpcDeadlineExceeded.ShouldBe(4);
	}

	[Fact]
	public void HaveCorrectGrpcNotFoundConstant()
	{
		FirestoreStalePositionDetector.GrpcNotFound.ShouldBe(5);
	}

	[Fact]
	public void HaveCorrectGrpcPermissionDeniedConstant()
	{
		FirestoreStalePositionDetector.GrpcPermissionDenied.ShouldBe(7);
	}

	[Fact]
	public void HaveCorrectGrpcResourceExhaustedConstant()
	{
		FirestoreStalePositionDetector.GrpcResourceExhausted.ShouldBe(8);
	}

	[Fact]
	public void HaveCorrectGrpcAbortedConstant()
	{
		FirestoreStalePositionDetector.GrpcAborted.ShouldBe(10);
	}

	[Fact]
	public void HaveCorrectGrpcInternalConstant()
	{
		FirestoreStalePositionDetector.GrpcInternal.ShouldBe(13);
	}

	[Fact]
	public void HaveCorrectGrpcUnavailableConstant()
	{
		FirestoreStalePositionDetector.GrpcUnavailable.ShouldBe(14);
	}

	#endregion

	#region StalePositionStatusCodes Tests

	[Fact]
	public void StalePositionStatusCodes_ContainsAllExpectedCodes()
	{
		// Assert
		var codes = FirestoreStalePositionDetector.StalePositionStatusCodes;

		codes.ShouldContain(1);  // CANCELLED
		codes.ShouldContain(4);  // DEADLINE_EXCEEDED
		codes.ShouldContain(5);  // NOT_FOUND
		codes.ShouldContain(7);  // PERMISSION_DENIED
		codes.ShouldContain(8);  // RESOURCE_EXHAUSTED
		codes.ShouldContain(10); // ABORTED
		codes.ShouldContain(13); // INTERNAL
		codes.ShouldContain(14); // UNAVAILABLE
	}

	[Fact]
	public void StalePositionStatusCodes_HasCorrectCount()
	{
		// Assert
		FirestoreStalePositionDetector.StalePositionStatusCodes.Count.ShouldBe(8);
	}

	#endregion

	#region IsStalePositionStatusCode Tests

	[Theory]
	[InlineData(1, true)]   // CANCELLED
	[InlineData(4, true)]   // DEADLINE_EXCEEDED
	[InlineData(5, true)]   // NOT_FOUND
	[InlineData(7, true)]   // PERMISSION_DENIED
	[InlineData(8, true)]   // RESOURCE_EXHAUSTED
	[InlineData(10, true)]  // ABORTED
	[InlineData(13, true)]  // INTERNAL
	[InlineData(14, true)]  // UNAVAILABLE
	[InlineData(0, false)]  // OK
	[InlineData(2, false)]  // UNKNOWN
	[InlineData(3, false)]  // INVALID_ARGUMENT
	[InlineData(6, false)]  // ALREADY_EXISTS
	[InlineData(9, false)]  // FAILED_PRECONDITION
	[InlineData(11, false)] // OUT_OF_RANGE
	[InlineData(12, false)] // UNIMPLEMENTED
	[InlineData(15, false)] // DATA_LOSS
	[InlineData(16, false)] // UNAUTHENTICATED
	public void IsStalePositionStatusCode_ReturnsCorrectResult(int statusCode, bool expected)
	{
		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionStatusCode(statusCode);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(StatusCode.Cancelled, true)]
	[InlineData(StatusCode.DeadlineExceeded, true)]
	[InlineData(StatusCode.NotFound, true)]
	[InlineData(StatusCode.PermissionDenied, true)]
	[InlineData(StatusCode.ResourceExhausted, true)]
	[InlineData(StatusCode.Aborted, true)]
	[InlineData(StatusCode.Internal, true)]
	[InlineData(StatusCode.Unavailable, true)]
	[InlineData(StatusCode.OK, false)]
	[InlineData(StatusCode.Unknown, false)]
	[InlineData(StatusCode.InvalidArgument, false)]
	public void IsStalePositionStatusCode_WithStatusCodeEnum_ReturnsCorrectResult(StatusCode statusCode, bool expected)
	{
		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionStatusCode(statusCode);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region IsStalePositionException Tests

	[Fact]
	public void IsStalePositionException_ReturnsFalse_WhenExceptionIsNull()
	{
		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(null);

		// Assert
		result.ShouldBeFalse();
	}

	[Theory]
	[InlineData(StatusCode.Cancelled)]
	[InlineData(StatusCode.DeadlineExceeded)]
	[InlineData(StatusCode.NotFound)]
	[InlineData(StatusCode.PermissionDenied)]
	[InlineData(StatusCode.ResourceExhausted)]
	[InlineData(StatusCode.Aborted)]
	[InlineData(StatusCode.Internal)]
	[InlineData(StatusCode.Unavailable)]
	public void IsStalePositionException_ReturnsTrue_ForRpcExceptionWithStaleStatusCode(StatusCode statusCode)
	{
		// Arrange
		var exception = new RpcException(new Status(statusCode, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(StatusCode.OK)]
	[InlineData(StatusCode.Unknown)]
	[InlineData(StatusCode.InvalidArgument)]
	public void IsStalePositionException_ReturnsFalse_ForRpcExceptionWithNonStaleStatusCode(StatusCode statusCode)
	{
		// Arrange
		var exception = new RpcException(new Status(statusCode, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForAggregateExceptionWithStaleInnerException()
	{
		// Arrange
		var inner = new RpcException(new Status(StatusCode.NotFound, "Test"));
		var exception = new AggregateException(inner);

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForExceptionWithStaleMessagePattern()
	{
		// Arrange
		var exception = new InvalidOperationException("DEADLINE exceeded");

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsTrue_ForExceptionWithStaleInnerException()
	{
		// Arrange
		var inner = new RpcException(new Status(StatusCode.Unavailable, "Test"));
		var exception = new InvalidOperationException("Outer", inner);

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsStalePositionException_ReturnsFalse_ForGenericException()
	{
		// Arrange
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = FirestoreStalePositionDetector.IsStalePositionException(exception);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GetStalePositionStatusCode Tests

	[Fact]
	public void GetStalePositionStatusCode_ReturnsNull_WhenExceptionIsNull()
	{
		// Act
		var result = FirestoreStalePositionDetector.GetStalePositionStatusCode(null);

		// Assert
		result.ShouldBeNull();
	}

	[Theory]
	[InlineData(StatusCode.Cancelled, 1)]
	[InlineData(StatusCode.DeadlineExceeded, 4)]
	[InlineData(StatusCode.NotFound, 5)]
	[InlineData(StatusCode.PermissionDenied, 7)]
	[InlineData(StatusCode.Unavailable, 14)]
	public void GetStalePositionStatusCode_ReturnsStatusCode_ForRpcException(StatusCode statusCode, int expected)
	{
		// Arrange
		var exception = new RpcException(new Status(statusCode, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.GetStalePositionStatusCode(exception);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void GetStalePositionStatusCode_ReturnsNull_ForNonStaleRpcException()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.OK, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.GetStalePositionStatusCode(exception);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetStalePositionStatusCode_ReturnsStatusCode_FromAggregateException()
	{
		// Arrange
		var inner = new RpcException(new Status(StatusCode.NotFound, "Test"));
		var exception = new AggregateException(inner);

		// Act
		var result = FirestoreStalePositionDetector.GetStalePositionStatusCode(exception);

		// Assert
		result.ShouldBe(5);
	}

	#endregion

	#region GetReasonCode Tests

	[Fact]
	public void GetReasonCode_ReturnsUnknown_WhenExceptionIsNull()
	{
		// Act
		var result = FirestoreStalePositionDetector.GetReasonCode(null);

		// Assert
		result.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	[Theory]
	[InlineData(StatusCode.Cancelled, "FIRESTORE_CANCELLED")]
	[InlineData(StatusCode.DeadlineExceeded, "FIRESTORE_DEADLINE_EXCEEDED")]
	[InlineData(StatusCode.NotFound, "FIRESTORE_NOT_FOUND")]
	[InlineData(StatusCode.PermissionDenied, "FIRESTORE_PERMISSION_DENIED")]
	[InlineData(StatusCode.ResourceExhausted, "FIRESTORE_RESOURCE_EXHAUSTED")]
	[InlineData(StatusCode.Aborted, "FIRESTORE_ABORTED")]
	[InlineData(StatusCode.Internal, "FIRESTORE_INTERNAL")]
	[InlineData(StatusCode.Unavailable, "FIRESTORE_UNAVAILABLE")]
	public void GetReasonCode_ReturnsCorrectCode_ForRpcException(StatusCode statusCode, string expected)
	{
		// Arrange
		var exception = new RpcException(new Status(statusCode, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.GetReasonCode(exception);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region CreateEventArgs Tests

	[Fact]
	public void CreateEventArgs_ThrowsArgumentNullException_WhenExceptionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			FirestoreStalePositionDetector.CreateEventArgs(null!, TestProcessorId));
	}

	[Fact]
	public void CreateEventArgs_ThrowsArgumentException_WhenProcessorIdIsNull()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			FirestoreStalePositionDetector.CreateEventArgs(exception, null!));
	}

	[Fact]
	public void CreateEventArgs_ThrowsArgumentException_WhenProcessorIdIsEmpty()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			FirestoreStalePositionDetector.CreateEventArgs(exception, ""));
	}

	[Fact]
	public void CreateEventArgs_SetsProcessorId()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);

		// Assert
		result.ProcessorId.ShouldBe(TestProcessorId);
	}

	[Fact]
	public void CreateEventArgs_SetsProviderType()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);

		// Assert
		result.ProviderType.ShouldBe("Firestore");
	}

	[Fact]
	public void CreateEventArgs_SetsReasonCode()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);

		// Assert
		result.ReasonCode.ShouldBe(FirestoreStalePositionReasonCodes.NotFound);
	}

	[Fact]
	public void CreateEventArgs_SetsOriginalException()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);

		// Assert
		result.OriginalException.ShouldBe(exception);
	}

	[Fact]
	public void CreateEventArgs_SetsDetectedAt()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);
		var after = DateTimeOffset.UtcNow;

		// Assert
		result.DetectedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.DetectedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void CreateEventArgs_SetsAdditionalContext_WithGrpcStatusCode()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(exception, TestProcessorId);

		// Assert
		result.AdditionalContext.ShouldNotBeNull();
		result.AdditionalContext["GrpcStatusCode"].ShouldBe(5);
	}

	[Fact]
	public void CreateEventArgs_IncludesProjectId_WhenProvided()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(
			exception, TestProcessorId, projectId: "my-project");

		// Assert
		result.AdditionalContext.ShouldNotBeNull();
		result.AdditionalContext["ProjectId"].ShouldBe("my-project");
	}

	[Fact]
	public void CreateEventArgs_IncludesCollectionPath_WhenProvided()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(
			exception, TestProcessorId, collectionPath: TestCollectionPath);

		// Assert
		result.AdditionalContext.ShouldNotBeNull();
		result.AdditionalContext["CollectionPath"].ShouldBe(TestCollectionPath);
		result.CaptureInstance.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public void CreateEventArgs_IncludesDocumentId_WhenProvided()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(
			exception, TestProcessorId, documentId: "doc-123");

		// Assert
		result.AdditionalContext.ShouldNotBeNull();
		result.AdditionalContext["DocumentId"].ShouldBe("doc-123");
	}

	[Fact]
	public void CreateEventArgs_SetsStalePosition_WhenProvided()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(
			exception, TestProcessorId, stalePosition: position);

		// Assert
		result.StalePosition.ShouldNotBeNull();
	}

	[Fact]
	public void CreateEventArgs_SetsNewPosition_WhenProvided()
	{
		// Arrange
		var exception = new RpcException(new Status(StatusCode.NotFound, "Test"));
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Act
		var result = FirestoreStalePositionDetector.CreateEventArgs(
			exception, TestProcessorId, newPosition: position);

		// Assert
		result.NewPosition.ShouldNotBeNull();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(FirestoreStalePositionDetector).IsAbstract.ShouldBeTrue();
		typeof(FirestoreStalePositionDetector).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(FirestoreStalePositionDetector).IsPublic.ShouldBeTrue();
	}

	#endregion
}
