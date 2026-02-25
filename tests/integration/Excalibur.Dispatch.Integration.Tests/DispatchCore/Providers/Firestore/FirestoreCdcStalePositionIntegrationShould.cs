// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Firestore.Cdc;

using Grpc.Core;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;

/// <summary>
/// Integration tests for Firestore CDC stale position detection and recovery.
/// Tests the <see cref="FirestoreStalePositionDetector"/> and <see cref="FirestoreCdcRecoveryOptions"/>
/// against mocked gRPC status code scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 178 - Cloud CDC Testing Epic.
/// bd-25xyv: Firestore CDC Stale Position Tests (4 tests).
/// </para>
/// <para>
/// These tests verify that the Firestore CDC stale position detection correctly identifies
/// gRPC status codes (CANCELLED, DEADLINE_EXCEEDED, NOT_FOUND, PERMISSION_DENIED,
/// RESOURCE_EXHAUSTED, ABORTED, INTERNAL, UNAVAILABLE) and that the recovery infrastructure
/// handles them properly.
/// </para>
/// <para>
/// Note: Firestore emulators cannot simulate stale listener position scenarios.
/// These tests use mock-based exception testing to verify the detection and recovery
/// infrastructure without requiring real Firestore connections.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "CDC")]
[Trait("Provider", "Firestore")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class FirestoreCdcStalePositionIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that the Firestore CDC processor correctly detects stale position scenarios
	/// from gRPC status codes indicating listener stream issues.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The FirestoreStalePositionDetector correctly identifies CDC-related gRPC status codes
	/// 2. Status code constants are properly defined (1, 4, 5, 7, 8, 10, 13, 14)
	/// 3. IsStalePositionStatusCode returns correct results for different codes
	/// </para>
	/// </remarks>
	[Fact]
	public void DetectStalePosition_FromGrpcStatusCodes()
	{
		// Arrange & Act: Verify status code constants
		FirestoreStalePositionDetector.GrpcCancelled.ShouldBe(1);
		FirestoreStalePositionDetector.GrpcDeadlineExceeded.ShouldBe(4);
		FirestoreStalePositionDetector.GrpcNotFound.ShouldBe(5);
		FirestoreStalePositionDetector.GrpcPermissionDenied.ShouldBe(7);
		FirestoreStalePositionDetector.GrpcResourceExhausted.ShouldBe(8);
		FirestoreStalePositionDetector.GrpcAborted.ShouldBe(10);
		FirestoreStalePositionDetector.GrpcInternal.ShouldBe(13);
		FirestoreStalePositionDetector.GrpcUnavailable.ShouldBe(14);

		// Assert: Verify all status codes are in the detection set
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(1);  // CANCELLED
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(4);  // DEADLINE_EXCEEDED
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(5);  // NOT_FOUND
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(7);  // PERMISSION_DENIED
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(8);  // RESOURCE_EXHAUSTED
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(10); // ABORTED
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(13); // INTERNAL
		FirestoreStalePositionDetector.StalePositionStatusCodes.ShouldContain(14); // UNAVAILABLE
		FirestoreStalePositionDetector.StalePositionStatusCodes.Count.ShouldBe(8);

		// Verify IsStalePositionStatusCode method with int codes
		FirestoreStalePositionDetector.IsStalePositionStatusCode(1).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(4).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(5).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(7).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(8).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(10).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(13).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(14).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(0).ShouldBeFalse();  // OK
		FirestoreStalePositionDetector.IsStalePositionStatusCode(2).ShouldBeFalse();  // UNKNOWN
		FirestoreStalePositionDetector.IsStalePositionStatusCode(3).ShouldBeFalse();  // INVALID_ARGUMENT

		// Verify IsStalePositionStatusCode with StatusCode enum
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.Cancelled).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.DeadlineExceeded).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.NotFound).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.PermissionDenied).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.ResourceExhausted).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.Aborted).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.Internal).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.Unavailable).ShouldBeTrue();
		FirestoreStalePositionDetector.IsStalePositionStatusCode(StatusCode.OK).ShouldBeFalse();

		// Verify IsStalePositionException with message-based detection
		var deadlineException = new InvalidOperationException("deadline exceeded");
		FirestoreStalePositionDetector.IsStalePositionException(deadlineException).ShouldBeTrue();

		var unavailableException = new InvalidOperationException("service unavailable");
		FirestoreStalePositionDetector.IsStalePositionException(unavailableException).ShouldBeTrue();

		var cancelledException = new InvalidOperationException("request cancelled");
		FirestoreStalePositionDetector.IsStalePositionException(cancelledException).ShouldBeTrue();

		var normalException = new InvalidOperationException("normal database error");
		FirestoreStalePositionDetector.IsStalePositionException(normalException).ShouldBeFalse();

		// Verify null exception handling
		FirestoreStalePositionDetector.IsStalePositionException(null).ShouldBeFalse();
	}

	/// <summary>
	/// Tests that reason codes are correctly mapped from gRPC status codes
	/// and error message patterns for Firestore CDC scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. FromGrpcStatusCode correctly maps gRPC status codes to reason codes
	/// 2. FromErrorMessage correctly identifies reason codes from error messages
	/// 3. Unknown status codes and messages return FIRESTORE_UNKNOWN
	/// </para>
	/// </remarks>
	[Fact]
	public void MapReasonCodes_FromGrpcStatusCodesAndMessages()
	{
		// Act & Assert: Status code mapping
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(1)
			.ShouldBe(FirestoreStalePositionReasonCodes.Cancelled);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(4)
			.ShouldBe(FirestoreStalePositionReasonCodes.DeadlineExceeded);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(5)
			.ShouldBe(FirestoreStalePositionReasonCodes.NotFound);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(7)
			.ShouldBe(FirestoreStalePositionReasonCodes.PermissionDenied);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(8)
			.ShouldBe(FirestoreStalePositionReasonCodes.ResourceExhausted);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(10)
			.ShouldBe(FirestoreStalePositionReasonCodes.Aborted);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(13)
			.ShouldBe(FirestoreStalePositionReasonCodes.Internal);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(14)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unavailable);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(0)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
		FirestoreStalePositionReasonCodes.FromGrpcStatusCode(999)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);

		// Act & Assert: Error message mapping - Deadline/timeout scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("deadline exceeded")
			.ShouldBe(FirestoreStalePositionReasonCodes.DeadlineExceeded);
		FirestoreStalePositionReasonCodes.FromErrorMessage("request timeout occurred")
			.ShouldBe(FirestoreStalePositionReasonCodes.DeadlineExceeded);

		// NotFound scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("collection not found")
			.ShouldBe(FirestoreStalePositionReasonCodes.NotFound);
		FirestoreStalePositionReasonCodes.FromErrorMessage("document path notfound")
			.ShouldBe(FirestoreStalePositionReasonCodes.NotFound);

		// Permission scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("permission denied")
			.ShouldBe(FirestoreStalePositionReasonCodes.PermissionDenied);
		FirestoreStalePositionReasonCodes.FromErrorMessage("access denied to resource")
			.ShouldBe(FirestoreStalePositionReasonCodes.PermissionDenied);
		FirestoreStalePositionReasonCodes.FromErrorMessage("unauthorized access")
			.ShouldBe(FirestoreStalePositionReasonCodes.PermissionDenied);

		// Unavailable scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("service unavailable")
			.ShouldBe(FirestoreStalePositionReasonCodes.Unavailable);
		FirestoreStalePositionReasonCodes.FromErrorMessage("service down temporarily")
			.ShouldBe(FirestoreStalePositionReasonCodes.Unavailable);

		// Cancelled scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("request cancelled by client")
			.ShouldBe(FirestoreStalePositionReasonCodes.Cancelled);
		FirestoreStalePositionReasonCodes.FromErrorMessage("operation was canceled")
			.ShouldBe(FirestoreStalePositionReasonCodes.Cancelled);

		// Resource exhausted scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("quota exhausted")
			.ShouldBe(FirestoreStalePositionReasonCodes.ResourceExhausted);
		FirestoreStalePositionReasonCodes.FromErrorMessage("rate limit exceeded")
			.ShouldBe(FirestoreStalePositionReasonCodes.ResourceExhausted);

		// Aborted scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("transaction aborted")
			.ShouldBe(FirestoreStalePositionReasonCodes.Aborted);
		FirestoreStalePositionReasonCodes.FromErrorMessage("conflict detected")
			.ShouldBe(FirestoreStalePositionReasonCodes.Aborted);

		// Internal scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("internal server error")
			.ShouldBe(FirestoreStalePositionReasonCodes.Internal);

		// Unknown scenarios
		FirestoreStalePositionReasonCodes.FromErrorMessage("some random error")
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
		FirestoreStalePositionReasonCodes.FromErrorMessage(null)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
		FirestoreStalePositionReasonCodes.FromErrorMessage("")
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}

	/// <summary>
	/// Tests that event args are correctly created from exceptions and context
	/// for Firestore CDC stale position scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. CreateEventArgs populates all required fields
	/// 2. Optional fields (projectId, collectionPath, documentId) are correctly handled
	/// 3. DetectedAt timestamp is reasonable
	/// 4. AdditionalContext contains gRPC status code when available
	/// </para>
	/// </remarks>
	[Fact]
	public void CreateEventArgs_WithCorrectProperties()
	{
		// Arrange
		var simulatedException = new InvalidOperationException(
			"Simulated listener deadline exceeded scenario");
		var collectionPath = "users/123/orders";
		var stalePosition = FirestoreCdcPosition.FromUpdateTime(
			collectionPath,
			DateTimeOffset.UtcNow.AddHours(-1),
			"old_doc_12345");
		var newPosition = FirestoreCdcPosition.Now(collectionPath);

		// Act
		var eventArgs = FirestoreStalePositionDetector.CreateEventArgs(
			simulatedException,
			"firestore-test-processor",
			stalePosition: stalePosition,
			newPosition: newPosition,
			projectId: "test-project-id",
			collectionPath: collectionPath,
			documentId: "order-456");

		// Assert: Required properties
		eventArgs.ProcessorId.ShouldBe("firestore-test-processor");
		eventArgs.ProviderType.ShouldBe("Firestore");
		eventArgs.ReasonCode.ShouldNotBeNullOrWhiteSpace();
		eventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Optional properties (now mapped to core type properties)
		_ = eventArgs.StalePosition.ShouldNotBeNull();
		_ = eventArgs.NewPosition.ShouldNotBeNull();
		eventArgs.DatabaseName.ShouldBe("test-project-id"); // ProjectId mapped to DatabaseName
		eventArgs.CaptureInstance.ShouldBe(collectionPath); // CollectionPath mapped to CaptureInstance

		// DocumentId is now in AdditionalContext
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext.ShouldContainKey("DocumentId");
		eventArgs.AdditionalContext["DocumentId"].ShouldBe("order-456");

		// Assert: Timestamp is reasonable (within last minute)
		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));
	}

	/// <summary>
	/// Tests that recovery options are correctly validated and callbacks are invoked
	/// for Firestore CDC stale position recovery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. Recovery options can be configured with various strategies
	/// 2. InvokeCallback strategy requires OnPositionReset callback
	/// 3. Callbacks receive correct event args when invoked
	/// 4. Validation fails for invalid configurations
	/// 5. Firestore-specific options (ReconnectionTimeout, MaxUnavailableWaitTime) work correctly
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ValidateRecoveryOptions_AndInvokeCallbacks()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			AutoReconnectOnDisconnect = true,
			RetryOnPermissionDenied = false,
			AlwaysInvokeCallbackOnReset = true,
			ReconnectionTimeout = TimeSpan.FromSeconds(30),
			MaxUnavailableWaitTime = TimeSpan.FromMinutes(5),
			OnPositionReset = (args, ct) =>
			{
				callbackInvoked = true;
				receivedEventArgs = args;
				return Task.CompletedTask;
			}
		};

		// Act: Validate options - should not throw
		recoveryOptions.Validate();

		// Create event args for callback test
		var simulatedException = new InvalidOperationException("Simulated permission denied");
		var eventArgs = FirestoreStalePositionDetector.CreateEventArgs(
			simulatedException,
			"permission-denied-processor",
			projectId: "callback-test-project",
			collectionPath: "protected/collection");

		// Invoke callback
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Callback was invoked with correct parameters
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("permission-denied-processor");
		receivedEventArgs.DatabaseName.ShouldBe("callback-test-project"); // ProjectId mapped to DatabaseName
		receivedEventArgs.CaptureInstance.ShouldBe("protected/collection"); // CollectionPath mapped to CaptureInstance
		receivedEventArgs.ProviderType.ShouldBe("Firestore");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Recovery options are correctly configured
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.AutoReconnectOnDisconnect.ShouldBeTrue();
		recoveryOptions.RetryOnPermissionDenied.ShouldBeFalse();
		recoveryOptions.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
		recoveryOptions.ReconnectionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		recoveryOptions.MaxUnavailableWaitTime.ShouldBe(TimeSpan.FromMinutes(5));

		// Verify other recovery strategies can be configured
		var throwOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw
		};
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest
		};
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());

		// Verify invalid MaxRecoveryAttempts throws
		var invalidAttemptsOptions = new FirestoreCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidAttemptsOptions.Validate());

		// Verify negative RecoveryAttemptDelay throws
		var invalidDelayOptions = new FirestoreCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidDelayOptions.Validate());

		// Verify negative ReconnectionTimeout throws
		var invalidReconnectOptions = new FirestoreCdcRecoveryOptions
		{
			ReconnectionTimeout = TimeSpan.FromSeconds(-1)
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidReconnectOptions.Validate());

		// Verify negative MaxUnavailableWaitTime throws
		var invalidWaitOptions = new FirestoreCdcRecoveryOptions
		{
			MaxUnavailableWaitTime = TimeSpan.FromMinutes(-1)
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidWaitOptions.Validate());

		// Verify GetReasonCode helper method
		var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Service down"));
		FirestoreStalePositionDetector.GetReasonCode(rpcException)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unavailable);
		FirestoreStalePositionDetector.GetReasonCode(null)
			.ShouldBe(FirestoreStalePositionReasonCodes.Unknown);
	}
}
