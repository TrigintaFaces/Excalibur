// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.CosmosDb.Cdc;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.CosmosDb;

/// <summary>
/// Integration tests for CosmosDB CDC stale position detection and recovery.
/// Tests the <see cref="CosmosDbStalePositionDetector"/> and <see cref="CosmosDbCdcRecoveryOptions"/>
/// against mocked HTTP status code scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 178 - Cloud CDC Testing Epic.
/// bd-uv872: CosmosDB CDC Stale Position Tests (4 tests).
/// </para>
/// <para>
/// These tests verify that the CosmosDB CDC stale position detection correctly identifies
/// HTTP status codes (410 Gone, 404 NotFound, 412 PreconditionFailed) and that the
/// recovery infrastructure handles them properly.
/// </para>
/// <para>
/// Note: CosmosDB emulators cannot simulate stale continuation token scenarios.
/// These tests use mock-based exception testing to verify the detection and recovery
/// infrastructure without requiring real CosmosDB connections.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "CDC")]
[Trait("Provider", "CosmosDB")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class CosmosDbCdcStalePositionIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that the CosmosDB CDC processor correctly detects stale position scenarios
	/// from HTTP status codes indicating continuation token expiry or partition issues.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The CosmosDbStalePositionDetector correctly identifies CDC-related HTTP status codes
	/// 2. Status code constants are properly defined (410, 404, 412)
	/// 3. Reason code mapping from HTTP status codes works correctly
	/// </para>
	/// </remarks>
	[Fact]
	public void DetectStalePosition_FromHttpStatusCodes()
	{
		// Arrange & Act: Verify status code constants
		CosmosDbStalePositionDetector.HttpGone.ShouldBe(410);
		CosmosDbStalePositionDetector.HttpNotFound.ShouldBe(404);
		CosmosDbStalePositionDetector.HttpPreconditionFailed.ShouldBe(412);

		// Assert: Verify all status codes are in the detection set
		CosmosDbStalePositionDetector.StalePositionStatusCodes.ShouldContain(410);
		CosmosDbStalePositionDetector.StalePositionStatusCodes.ShouldContain(404);
		CosmosDbStalePositionDetector.StalePositionStatusCodes.ShouldContain(412);
		CosmosDbStalePositionDetector.StalePositionStatusCodes.Count.ShouldBe(3);

		// Verify IsStalePositionStatusCode method
		CosmosDbStalePositionDetector.IsStalePositionStatusCode(410).ShouldBeTrue();
		CosmosDbStalePositionDetector.IsStalePositionStatusCode(404).ShouldBeTrue();
		CosmosDbStalePositionDetector.IsStalePositionStatusCode(412).ShouldBeTrue();
		CosmosDbStalePositionDetector.IsStalePositionStatusCode(200).ShouldBeFalse();
		CosmosDbStalePositionDetector.IsStalePositionStatusCode(500).ShouldBeFalse();
	}

	/// <summary>
	/// Tests that reason codes are correctly mapped from HTTP status codes
	/// and error message patterns for CosmosDB CDC scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. FromStatusCode correctly maps HTTP status codes to reason codes
	/// 2. FromErrorMessage correctly identifies reason codes from error messages
	/// 3. Unknown status codes and messages return COSMOSDB_UNKNOWN
	/// </para>
	/// </remarks>
	[Fact]
	public void MapReasonCodes_FromStatusCodesAndMessages()
	{
		// Act & Assert: Status code mapping
		CosmosDbStalePositionReasonCodes.FromStatusCode(410)
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
		CosmosDbStalePositionReasonCodes.FromStatusCode(404)
			.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionNotFound);
		CosmosDbStalePositionReasonCodes.FromStatusCode(412)
			.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
		CosmosDbStalePositionReasonCodes.FromStatusCode(500)
			.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);

		// Act & Assert: Error message mapping - Continuation token scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("continuation token expired")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("continuation token is invalid")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("continuation token gone")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);

		// Partition scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("partition not found")
			.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionNotFound);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("partition split detected")
			.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionSplit);

		// Container scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("container not found")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContainerDeleted);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("container deleted")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContainerDeleted);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("container does not exist")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ContainerDeleted);

		// ETag scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("ETag mismatch detected")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("precondition failed")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);

		// Throughput scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("throughput change detected")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ThroughputChange);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("RU change in progress")
			.ShouldBe(CosmosDbStalePositionReasonCodes.ThroughputChange);

		// Unknown scenarios
		CosmosDbStalePositionReasonCodes.FromErrorMessage("some random error")
			.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
		CosmosDbStalePositionReasonCodes.FromErrorMessage(null)
			.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
		CosmosDbStalePositionReasonCodes.FromErrorMessage("")
			.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
	}

	/// <summary>
	/// Tests that event args are correctly created from exceptions and context
	/// for CosmosDB CDC stale position scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. CreateEventArgs populates all required fields
	/// 2. Optional fields are correctly handled
	/// 3. DetectedAt timestamp is reasonable
	/// 4. AdditionalContext contains HTTP status code when available
	/// </para>
	/// </remarks>
	[Fact]
	public void CreateEventArgs_WithCorrectProperties()
	{
		// Arrange
		var simulatedException = new InvalidOperationException(
			"Simulated continuation token expired scenario");
		var stalePosition = CosmosDbCdcPosition.FromContinuationToken("old_continuation_token_12345");
		var newPosition = CosmosDbCdcPosition.Beginning();

		// Act
		var eventArgs = CosmosDbStalePositionDetector.CreateEventArgs(
			simulatedException,
			"cosmos-test-processor",
			stalePosition: stalePosition,
			newPosition: newPosition,
			databaseName: "test-database",
			containerName: "test-container",
			partitionKeyRangeId: "0");

		// Assert: Required properties (now using CdcPositionResetEventArgs)
		eventArgs.ProcessorId.ShouldBe("cosmos-test-processor");
		eventArgs.ProviderType.ShouldBe("CosmosDB");
		eventArgs.ReasonCode.ShouldNotBeNullOrWhiteSpace();
		eventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Core properties - positions now stored as byte[]
		eventArgs.StalePosition.ShouldNotBeNull();
		eventArgs.NewPosition.ShouldNotBeNull();

		// Assert: Database and capture instance mapping
		eventArgs.DatabaseName.ShouldBe("test-database");
		eventArgs.CaptureInstance.ShouldBe("test-database/test-container"); // Combined database/container

		// Assert: Provider-specific properties now in AdditionalContext
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext.ShouldContainKey("ContainerName");
		eventArgs.AdditionalContext["ContainerName"].ShouldBe("test-container");
		eventArgs.AdditionalContext.ShouldContainKey("PartitionKeyRangeId");
		eventArgs.AdditionalContext["PartitionKeyRangeId"].ShouldBe("0");

		// Assert: Timestamp is reasonable (within last minute)
		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));

		// Assert: ToString provides useful info for logging
		var toStringResult = eventArgs.ToString();
		toStringResult.ShouldContain("cosmos-test-processor");
		toStringResult.ShouldContain("CosmosDB");
		toStringResult.ShouldContain("test-database/test-container");
	}

	/// <summary>
	/// Tests that recovery options are correctly validated and callbacks are invoked
	/// for CosmosDB CDC stale position recovery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. Recovery options can be configured with various strategies
	/// 2. InvokeCallback strategy requires OnPositionReset callback
	/// 3. Callbacks receive correct event args when invoked
	/// 4. Validation fails for invalid configurations
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ValidateRecoveryOptions_AndInvokeCallbacks()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			AutoRecreateProcessorOnInvalidToken = true,
			UseCurrentTimeOnResumeFailure = true,
			HandlePartitionSplitsGracefully = true,
			AlwaysInvokeCallbackOnReset = true,
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
		var simulatedException = new InvalidOperationException("Simulated partition split");
		var eventArgs = CosmosDbStalePositionDetector.CreateEventArgs(
			simulatedException,
			"partition-split-processor",
			databaseName: "callback-test-db",
			containerName: "callback-test-container");

		// Invoke callback
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Callback was invoked with correct parameters (now using CdcPositionResetEventArgs)
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("partition-split-processor");
		receivedEventArgs.DatabaseName.ShouldBe("callback-test-db");
		receivedEventArgs.CaptureInstance.ShouldBe("callback-test-db/callback-test-container");

		// Container name now in AdditionalContext
		_ = receivedEventArgs.AdditionalContext.ShouldNotBeNull();
		receivedEventArgs.AdditionalContext.ShouldContainKey("ContainerName");
		receivedEventArgs.AdditionalContext["ContainerName"].ShouldBe("callback-test-container");

		receivedEventArgs.ProviderType.ShouldBe("CosmosDB");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Recovery options are correctly configured (now using StalePositionRecoveryStrategy)
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.AutoRecreateProcessorOnInvalidToken.ShouldBeTrue();
		recoveryOptions.UseCurrentTimeOnResumeFailure.ShouldBeTrue();
		recoveryOptions.HandlePartitionSplitsGracefully.ShouldBeTrue();
		recoveryOptions.AlwaysInvokeCallbackOnReset.ShouldBeTrue();

		// Verify other recovery strategies can be configured
		var throwOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw
		};
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest
		};
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());

		// Verify invalid MaxRecoveryAttempts throws
		var invalidAttemptsOptions = new CosmosDbCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidAttemptsOptions.Validate());

		// Verify negative RecoveryAttemptDelay throws
		var invalidDelayOptions = new CosmosDbCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidDelayOptions.Validate());
	}
}
