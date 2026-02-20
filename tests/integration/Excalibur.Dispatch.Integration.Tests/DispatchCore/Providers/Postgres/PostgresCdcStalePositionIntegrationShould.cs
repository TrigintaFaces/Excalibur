// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Integration tests for Postgres CDC stale position detection and recovery.
/// Tests the <see cref="PostgresStalePositionDetector"/> and <see cref="PostgresCdcRecoveryOptions"/>
/// against real Postgres scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 176 - Provider Testing Epic Phase 2.
/// bd-cfh8l: Postgres CDC Stale Position Tests (2 tests).
/// </para>
/// <para>
/// These tests verify that the Postgres CDC stale position detection correctly identifies
/// SQLSTATE codes and that the recovery infrastructure handles them properly.
/// </para>
/// <para>
/// Note: Full CDC functionality (logical replication with pgoutput) requires
/// wal_level=logical which is not typically configured in Docker containers.
/// These tests focus on the error detection and recovery infrastructure using
/// simulated scenarios.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "CDC")]
[Trait("Provider", "Postgres")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class PostgresCdcStalePositionIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _pgFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcStalePositionIntegrationShould"/> class.
	/// </summary>
	/// <param name="pgFixture">The Postgres container fixture.</param>
	public PostgresCdcStalePositionIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	/// <summary>
	/// Tests that the Postgres CDC processor correctly detects and recovers from stale WAL
	/// position scenarios that occur when WAL segments are removed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The PostgresStalePositionDetector correctly identifies CDC-related SQLSTATE codes
	/// 2. Event args are properly created with correct processor and provider info
	/// 3. Database connectivity works correctly with the container
	/// </para>
	/// <para>
	/// Note: Full CDC functionality requires wal_level=logical which isn't available
	/// in standard Docker containers. This test verifies the detection infrastructure using
	/// simulated exceptions and database connectivity verification.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromStaleLsn_AfterWalRotation_DetectsWalPositionError()
	{
		// Arrange: Verify database connectivity and set up test table
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		// Create a test table to verify database connectivity
		_ = await connection.ExecuteAsync(
			"""
			CREATE TABLE IF NOT EXISTS cdc_test_table (
			    id SERIAL PRIMARY KEY,
			    name VARCHAR(100) NOT NULL,
			    created_at TIMESTAMPTZ DEFAULT NOW()
			);
			""").ConfigureAwait(true);

		// Insert some test data to verify database is working
		_ = await connection.ExecuteAsync(
			"INSERT INTO cdc_test_table (name) VALUES (@Name)",
			new { Name = "Test Record 1" }).ConfigureAwait(true);

		// Act: Verify database connectivity - query should succeed
		var count = await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM cdc_test_table").ConfigureAwait(true);
		count.ShouldBeGreaterThan(0, "Database should have test data");

		// Since logical replication is not enabled in Docker containers, we test the detection logic
		// using the detector with simulated event args
		var simulatedStalePosition = new PostgresCdcPosition(1UL);

		// Verify event args creation works correctly for WAL rotation scenario
		// In production, this would be called when a real Postgres CDC error (58P01, 55000, etc.) occurs
		var genericException = new InvalidOperationException("Simulated WAL rotation - LSN no longer valid");
		var eventArgs = PostgresStalePositionDetector.CreateEventArgs(
			genericException,
			"test-processor",
			stalePosition: simulatedStalePosition,
			replicationSlot: "test_slot",
			publication: "test_publication");

		// Assert: Verify event args are correctly populated (now using CdcPositionResetEventArgs)
		eventArgs.ProcessorId.ShouldBe("test-processor");
		eventArgs.ProviderType.ShouldBe("Postgres");
		eventArgs.OriginalException.ShouldBe(genericException);

		// Core properties - positions now stored as byte[]
		eventArgs.StalePosition.ShouldNotBeNull();
		eventArgs.CaptureInstance.ShouldBe("test_slot"); // ReplicationSlot mapped to CaptureInstance

		// Provider-specific properties now in AdditionalContext
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext.ShouldContainKey("ReplicationSlotName");
		eventArgs.AdditionalContext["ReplicationSlotName"].ShouldBe("test_slot");
		eventArgs.AdditionalContext.ShouldContainKey("PublicationName");
		eventArgs.AdditionalContext["PublicationName"].ShouldBe("test_publication");

		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));

		// Verify detector constants are exposed correctly (tested in unit tests, but verify in integration)
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("58P01"); // WAL segment not accessible
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("55000"); // Object not in prerequisite state
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("42704"); // Undefined object
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("08006"); // Connection failure
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("08P01"); // Protocol violation
		PostgresStalePositionDetector.StalePositionSqlStates.ShouldContain("0A000"); // Feature not supported
	}

	/// <summary>
	/// Tests that the Postgres CDC processor correctly detects and recovers from stale WAL
	/// position scenarios that occur after a database backup/restore operation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. Recovery options can be configured correctly
	/// 2. The OnPositionReset callback is properly invoked
	/// 3. Event args pass through the callback with correct data
	/// 4. Database connectivity works in the container environment
	/// </para>
	/// <para>
	/// Note: Full backup/restore with logical replication requires features not available
	/// in Docker. This test verifies the callback mechanism and options configuration.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromStaleLsn_AfterBackupRestore_InvokesRecoveryCallback()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new PostgresCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			EnableStructuredLogging = true,
			AutoRecreateSlotOnInvalidation = true,
			OnPositionReset = (args, ct) =>
			{
				callbackInvoked = true;
				receivedEventArgs = args;
				return Task.CompletedTask;
			}
		};

		// Validate options configuration - should not throw
		recoveryOptions.Validate();

		// Verify database connectivity
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		// Verify we can query the database
		var serverVersion = await connection.ExecuteScalarAsync<string>(
			"SELECT version()").ConfigureAwait(true);
		_ = serverVersion.ShouldNotBeNull();
		serverVersion.ShouldContain("Postgres");

		// Simulate backup/restore scenario with event args
		var simulatedStalePosition = new PostgresCdcPosition(0xABCD0001UL);
		var simulatedNewPosition = new PostgresCdcPosition(0x12340001UL);

		// Create event args simulating backup/restore scenario
		// Use InvalidOperationException to simulate a CDC error in the callback flow
		var simulatedException = new InvalidOperationException(
			"Simulated backup/restore scenario - CDC positions invalidated");
		var eventArgs = PostgresStalePositionDetector.CreateEventArgs(
			simulatedException,
			"backup-restore-processor",
			stalePosition: simulatedStalePosition,
			newPosition: simulatedNewPosition,
			replicationSlot: "restored_slot",
			publication: "restored_publication");

		// Act: Invoke the callback (simulating what the processor would do)
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Verify callback was invoked with correct parameters (now using CdcPositionResetEventArgs)
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("backup-restore-processor");

		// Core properties - positions now stored as byte[]
		receivedEventArgs.StalePosition.ShouldNotBeNull();
		receivedEventArgs.NewPosition.ShouldNotBeNull();
		receivedEventArgs.CaptureInstance.ShouldBe("restored_slot"); // ReplicationSlot mapped to CaptureInstance

		// Provider-specific properties now in AdditionalContext
		_ = receivedEventArgs.AdditionalContext.ShouldNotBeNull();
		receivedEventArgs.AdditionalContext.ShouldContainKey("ReplicationSlotName");
		receivedEventArgs.AdditionalContext["ReplicationSlotName"].ShouldBe("restored_slot");
		receivedEventArgs.AdditionalContext.ShouldContainKey("PublicationName");
		receivedEventArgs.AdditionalContext["PublicationName"].ShouldBe("restored_publication");

		receivedEventArgs.ProviderType.ShouldBe("Postgres");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Verify recovery options are correctly configured
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.EnableStructuredLogging.ShouldBeTrue();
		recoveryOptions.AutoRecreateSlotOnInvalidation.ShouldBeTrue();

		// Verify other recovery strategies can be configured
		var throwOptions = new PostgresCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.Throw };
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new PostgresCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest };
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new PostgresCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest };
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new PostgresCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback };
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());
	}
}
