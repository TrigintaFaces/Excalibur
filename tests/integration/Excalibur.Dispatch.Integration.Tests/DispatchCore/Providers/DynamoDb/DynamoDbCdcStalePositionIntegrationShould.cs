// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.DynamoDb.Cdc;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.DynamoDb;

/// <summary>
/// Integration tests for DynamoDB Streams CDC stale position detection and recovery.
/// Tests the <see cref="DynamoDbStalePositionDetector"/> and <see cref="DynamoDbCdcRecoveryOptions"/>
/// against mocked AWS exception scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 178 - Cloud CDC Testing Epic.
/// bd-ijmpo: DynamoDB CDC Stale Position Tests (4 tests).
/// </para>
/// <para>
/// These tests verify that the DynamoDB CDC stale position detection correctly identifies
/// AWS exception types (ExpiredIteratorException, TrimmedDataAccessException, ResourceNotFoundException)
/// and that the recovery infrastructure handles them properly.
/// </para>
/// <para>
/// Note: DynamoDB LocalStack cannot simulate stale iterator or trimmed data scenarios.
/// These tests use mock-based exception testing to verify the detection and recovery
/// infrastructure without requiring real DynamoDB connections.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "CDC")]
[Trait("Provider", "DynamoDB")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class DynamoDbCdcStalePositionIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that the DynamoDB CDC processor correctly detects stale position scenarios
	/// from AWS exception types indicating iterator expiry or trimmed data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The DynamoDbStalePositionDetector correctly identifies CDC-related exception types
	/// 2. Exception type names are properly defined
	/// 3. IsStalePositionException returns correct results for different exception types
	/// </para>
	/// </remarks>
	[Fact]
	public void DetectStalePosition_FromExceptionTypes()
	{
		// Arrange & Act: Verify exception type constants
		DynamoDbStalePositionDetector.StalePositionExceptionTypes.ShouldContain("ExpiredIteratorException");
		DynamoDbStalePositionDetector.StalePositionExceptionTypes.ShouldContain("TrimmedDataAccessException");
		DynamoDbStalePositionDetector.StalePositionExceptionTypes.ShouldContain("ResourceNotFoundException");
		DynamoDbStalePositionDetector.StalePositionExceptionTypes.Count.ShouldBe(3);

		// Assert: Verify GetStalePositionExceptionType method with simulated exceptions
		// Note: We simulate by using the message-based detection since we can't instantiate real AWS exceptions
		var genericException = new InvalidOperationException("iterator expired - please refresh");
		var isStale = DynamoDbStalePositionDetector.IsStalePositionException(genericException);
		isStale.ShouldBeTrue("Message containing 'iterator expired' should be detected");

		var trimException = new InvalidOperationException("data trimmed beyond horizon");
		DynamoDbStalePositionDetector.IsStalePositionException(trimException).ShouldBeTrue();

		var shardException = new InvalidOperationException("shard not found");
		DynamoDbStalePositionDetector.IsStalePositionException(shardException).ShouldBeTrue();

		var normalException = new InvalidOperationException("normal database error");
		DynamoDbStalePositionDetector.IsStalePositionException(normalException).ShouldBeFalse();

		// Verify null exception handling
		DynamoDbStalePositionDetector.IsStalePositionException(null).ShouldBeFalse();

		// Verify aggregate exception handling
		var aggregateWithStale = new AggregateException(
			new InvalidOperationException("iterator expired"));
		DynamoDbStalePositionDetector.IsStalePositionException(aggregateWithStale).ShouldBeTrue();
	}

	/// <summary>
	/// Tests that reason codes are correctly mapped from exception types
	/// and error message patterns for DynamoDB Streams CDC scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. FromExceptionType correctly maps AWS exception types to reason codes
	/// 2. FromErrorMessage correctly identifies reason codes from error messages
	/// 3. Unknown exception types and messages return DYNAMODB_UNKNOWN
	/// </para>
	/// </remarks>
	[Fact]
	public void MapReasonCodes_FromExceptionTypesAndMessages()
	{
		// Act & Assert: Exception type mapping
		DynamoDbStalePositionReasonCodes.FromExceptionType("ExpiredIteratorException")
			.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);
		DynamoDbStalePositionReasonCodes.FromExceptionType("TrimmedDataAccessException")
			.ShouldBe(DynamoDbStalePositionReasonCodes.TrimmedData);
		DynamoDbStalePositionReasonCodes.FromExceptionType("ResourceNotFoundException")
			.ShouldBe(DynamoDbStalePositionReasonCodes.ShardNotFound);
		DynamoDbStalePositionReasonCodes.FromExceptionType("UnknownException")
			.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
		DynamoDbStalePositionReasonCodes.FromExceptionType(null)
			.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);

		// Act & Assert: Error message mapping - Iterator scenarios
		DynamoDbStalePositionReasonCodes.FromErrorMessage("iterator expired")
			.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("iterator invalid")
			.ShouldBe(DynamoDbStalePositionReasonCodes.IteratorExpired);

		// Trimmed data scenarios
		DynamoDbStalePositionReasonCodes.FromErrorMessage("data beyond trim horizon")
			.ShouldBe(DynamoDbStalePositionReasonCodes.TrimmedData);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("trimmed data access")
			.ShouldBe(DynamoDbStalePositionReasonCodes.TrimmedData);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("sequence out of range")
			.ShouldBe(DynamoDbStalePositionReasonCodes.TrimmedData);

		// Shard scenarios
		DynamoDbStalePositionReasonCodes.FromErrorMessage("shard not found")
			.ShouldBe(DynamoDbStalePositionReasonCodes.ShardNotFound);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("shard closed")
			.ShouldBe(DynamoDbStalePositionReasonCodes.ShardClosed);

		// Stream scenarios
		DynamoDbStalePositionReasonCodes.FromErrorMessage("stream not found")
			.ShouldBe(DynamoDbStalePositionReasonCodes.StreamNotFound);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("stream disabled")
			.ShouldBe(DynamoDbStalePositionReasonCodes.StreamDisabled);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("stream does not exist")
			.ShouldBe(DynamoDbStalePositionReasonCodes.StreamNotFound);

		// Unknown scenarios
		DynamoDbStalePositionReasonCodes.FromErrorMessage("some random error")
			.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
		DynamoDbStalePositionReasonCodes.FromErrorMessage(null)
			.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
		DynamoDbStalePositionReasonCodes.FromErrorMessage("")
			.ShouldBe(DynamoDbStalePositionReasonCodes.Unknown);
	}

	/// <summary>
	/// Tests that event args are correctly created from exceptions and context
	/// for DynamoDB Streams CDC stale position scenarios.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. CreateEventArgs populates all required fields
	/// 2. Optional fields (streamArn, tableName, shardId, sequenceNumber) are correctly handled
	/// 3. DetectedAt timestamp is reasonable
	/// 4. AdditionalContext contains exception type when available
	/// </para>
	/// </remarks>
	[Fact]
	public void CreateEventArgs_WithCorrectProperties()
	{
		// Arrange
		var simulatedException = new InvalidOperationException(
			"Simulated iterator expired scenario");
		var streamArn = "arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2025-01-01";
		var stalePosition = DynamoDbCdcPosition.FromShardPositions(
			streamArn,
			new Dictionary<string, string> { ["shard-001"] = "old_sequence_12345" });
		var newPosition = DynamoDbCdcPosition.Beginning(streamArn);

		// Act
		var eventArgs = DynamoDbStalePositionDetector.CreateEventArgs(
			simulatedException,
			"dynamodb-test-processor",
			stalePosition: stalePosition,
			newPosition: newPosition,
			streamArn: "arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2025-01-01",
			tableName: "TestTable",
			shardId: "shard-001",
			sequenceNumber: "old_sequence_12345");

		// Assert: Required properties (now using CdcPositionResetEventArgs)
		eventArgs.ProcessorId.ShouldBe("dynamodb-test-processor");
		eventArgs.ProviderType.ShouldBe("DynamoDB");
		eventArgs.ReasonCode.ShouldNotBeNullOrWhiteSpace();
		eventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Core properties - positions now stored as byte[]
		eventArgs.StalePosition.ShouldNotBeNull();
		eventArgs.NewPosition.ShouldNotBeNull();

		// Assert: CaptureInstance maps to table name
		eventArgs.CaptureInstance.ShouldBe("TestTable");

		// Assert: Provider-specific properties now in AdditionalContext
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext.ShouldContainKey("StreamArn");
		eventArgs.AdditionalContext["StreamArn"].ShouldBe("arn:aws:dynamodb:us-east-1:123456789:table/TestTable/stream/2025-01-01");
		eventArgs.AdditionalContext.ShouldContainKey("TableName");
		eventArgs.AdditionalContext["TableName"].ShouldBe("TestTable");
		eventArgs.AdditionalContext.ShouldContainKey("ShardId");
		eventArgs.AdditionalContext["ShardId"].ShouldBe("shard-001");
		eventArgs.AdditionalContext.ShouldContainKey("SequenceNumber");
		eventArgs.AdditionalContext["SequenceNumber"].ShouldBe("old_sequence_12345");

		// Assert: Timestamp is reasonable (within last minute)
		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));
	}

	/// <summary>
	/// Tests that recovery options are correctly validated and callbacks are invoked
	/// for DynamoDB Streams CDC stale position recovery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. Recovery options can be configured with various strategies
	/// 2. InvokeCallback strategy requires OnPositionReset callback
	/// 3. Callbacks receive correct event args when invoked
	/// 4. Validation fails for invalid configurations (negative delays, invalid refresh intervals)
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ValidateRecoveryOptions_AndInvokeCallbacks()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			AutoRefreshExpiredIterators = true,
			HandleShardSplitsGracefully = true,
			AlwaysInvokeCallbackOnReset = true,
			IteratorRefreshInterval = TimeSpan.FromMinutes(10),
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
		var simulatedException = new InvalidOperationException("Simulated shard split");
		var eventArgs = DynamoDbStalePositionDetector.CreateEventArgs(
			simulatedException,
			"shard-split-processor",
			streamArn: "arn:aws:dynamodb:us-east-1:123456789:table/SplitTable/stream/2025-01-01",
			tableName: "SplitTable",
			shardId: "shard-parent-001");

		// Invoke callback
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Callback was invoked with correct parameters (now using CdcPositionResetEventArgs)
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("shard-split-processor");

		// Provider-specific properties now in AdditionalContext
		_ = receivedEventArgs.AdditionalContext.ShouldNotBeNull();
		receivedEventArgs.AdditionalContext.ShouldContainKey("StreamArn");
		receivedEventArgs.AdditionalContext["StreamArn"].ShouldBe("arn:aws:dynamodb:us-east-1:123456789:table/SplitTable/stream/2025-01-01");
		receivedEventArgs.AdditionalContext.ShouldContainKey("TableName");
		receivedEventArgs.AdditionalContext["TableName"].ShouldBe("SplitTable");
		receivedEventArgs.AdditionalContext.ShouldContainKey("ShardId");
		receivedEventArgs.AdditionalContext["ShardId"].ShouldBe("shard-parent-001");

		receivedEventArgs.ProviderType.ShouldBe("DynamoDB");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Assert: Recovery options are correctly configured (now using StalePositionRecoveryStrategy)
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.AutoRefreshExpiredIterators.ShouldBeTrue();
		recoveryOptions.HandleShardSplitsGracefully.ShouldBeTrue();
		recoveryOptions.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
		recoveryOptions.IteratorRefreshInterval.ShouldBe(TimeSpan.FromMinutes(10));

		// Verify other recovery strategies can be configured
		var throwOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw
		};
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest
		};
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());

		// Verify invalid MaxRecoveryAttempts throws
		var invalidAttemptsOptions = new DynamoDbCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidAttemptsOptions.Validate());

		// Verify negative RecoveryAttemptDelay throws
		var invalidDelayOptions = new DynamoDbCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidDelayOptions.Validate());

		// Verify invalid IteratorRefreshInterval throws (must be between 0 and 14 minutes)
		var invalidRefreshOptions = new DynamoDbCdcRecoveryOptions
		{
			IteratorRefreshInterval = TimeSpan.FromMinutes(15) // Exceeds 14 minute limit
		};
		_ = Should.Throw<InvalidOperationException>(() => invalidRefreshOptions.Validate());
	}
}
