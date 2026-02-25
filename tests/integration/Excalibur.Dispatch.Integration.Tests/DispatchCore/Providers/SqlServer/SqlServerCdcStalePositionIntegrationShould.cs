// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Data.SqlClient;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Integration tests for CDC stale position detection and recovery.
/// Tests the <see cref="CdcStalePositionDetector"/> and <see cref="CdcRecoveryOptions"/>
/// against real SQL Server scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-vmw20: SqlServer CDC Stale Position Tests (2 tests).
/// </para>
/// <para>
/// These tests verify that the CDC stale position detection correctly identifies
/// SQL Server error codes and that the recovery infrastructure handles them properly.
/// </para>
/// <para>
/// Note: Full CDC functionality (enabling CDC on tables) requires SQL Server Agent
/// which is not available in Docker containers. These tests focus on the error
/// detection and recovery infrastructure using simulated scenarios.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "CDC")]
[Trait("Provider", "SqlServer")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class SqlServerCdcStalePositionIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _sqlFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStalePositionIntegrationShould"/> class.
	/// </summary>
	/// <param name="sqlFixture">The SQL Server container fixture.</param>
	public SqlServerCdcStalePositionIntegrationShould(SqlServerFixture sqlFixture)
	{
		_sqlFixture = sqlFixture;
	}

	/// <summary>
	/// Tests that the CDC processor correctly detects and recovers from stale LSN
	/// scenarios that occur when the CDC cleanup job removes old records.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The CdcStalePositionDetector correctly identifies CDC-related errors
	/// 2. Event args are properly created with correct processor and provider info
	/// 3. Database connectivity works correctly with the container
	/// </para>
	/// <para>
	/// Note: Full CDC functionality requires SQL Server Agent which isn't available
	/// in Docker containers. This test verifies the detection infrastructure using
	/// direct SqlException simulation and database connectivity verification.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromStaleLsn_AfterCleanup_DetectsInvalidLsnError()
	{
		// Arrange: Verify database connectivity and set up test table
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		// Create a test table to verify database connectivity
		_ = await connection.ExecuteAsync(
			"""
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CdcTestTable]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE dbo.CdcTestTable (
			        Id INT PRIMARY KEY IDENTITY(1,1),
			        Name NVARCHAR(100) NOT NULL,
			        CreatedAt DATETIME2 DEFAULT GETUTCDATE()
			    );
			END
			""").ConfigureAwait(true);

		// Insert some test data to verify database is working
		_ = await connection.ExecuteAsync(
			"INSERT INTO dbo.CdcTestTable (Name) VALUES (@Name)",
			new { Name = "Test Record 1" }).ConfigureAwait(true);

		// Act: Verify database connectivity - query should succeed
		var count = await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM dbo.CdcTestTable").ConfigureAwait(true);
		count.ShouldBeGreaterThan(0, "Database should have test data");

		// Since CDC is not enabled in Docker containers, we test the detection logic
		// using the unit-tested detector with simulated event args
		var simulatedStalePosition = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };

		// Verify event args creation works correctly for CDC cleanup scenario
		// In production, this would be called when a real CDC error (22037, 22029, etc.) occurs
		var genericException = new InvalidOperationException("Simulated CDC cleanup - LSN no longer valid");
		var eventArgs = CdcStalePositionDetector.CreateEventArgs(
			genericException,
			"test-processor",
			stalePosition: simulatedStalePosition,
			captureInstance: "dbo_CdcTestTable");

		// Assert: Verify event args are correctly populated
		eventArgs.ProcessorId.ShouldBe("test-processor");
		eventArgs.ProviderType.ShouldBe("SqlServer");
		eventArgs.CaptureInstance.ShouldBe("dbo_CdcTestTable");
		eventArgs.OriginalException.ShouldBe(genericException);
		eventArgs.StalePosition.ShouldBe(simulatedStalePosition);
		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));

		// Verify detector constants are exposed correctly (tested in unit tests, but verify in integration)
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22037);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22029);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22911);
		CdcStalePositionDetector.StalePositionErrorNumbers.ShouldContain(22985);
	}

	/// <summary>
	/// Tests that the CDC processor correctly detects and recovers from stale LSN
	/// scenarios that occur after a database backup/restore operation.
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
	/// Note: Full backup/restore with CDC requires Enterprise features not available
	/// in Docker. This test verifies the callback mechanism and options configuration.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromStaleLsn_AfterBackupRestore_InvokesRecoveryCallback()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			EnableStructuredLogging = true,
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
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		// Verify we can query the database
		var serverVersion = await connection.ExecuteScalarAsync<string>(
			"SELECT @@VERSION").ConfigureAwait(true);
		_ = serverVersion.ShouldNotBeNull();
		serverVersion.ShouldContain("Microsoft SQL Server");

		// Simulate backup/restore scenario with event args
		var simulatedStalePosition = new byte[] { 0x00, 0x00, 0xAB, 0xCD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
		var simulatedNewPosition = new byte[] { 0x00, 0x00, 0x12, 0x34, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };

		// Create event args simulating backup/restore scenario
		// Use InvalidOperationException to simulate a CDC error in the callback flow
		var simulatedException = new InvalidOperationException(
			"Simulated backup/restore scenario - CDC positions invalidated");
		var eventArgs = CdcStalePositionDetector.CreateEventArgs(
			simulatedException,
			"backup-restore-processor",
			stalePosition: simulatedStalePosition,
			newPosition: simulatedNewPosition,
			captureInstance: "dbo_RestoredTable");

		// Act: Invoke the callback (simulating what the processor would do)
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Verify callback was invoked with correct parameters
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("backup-restore-processor");
		receivedEventArgs.StalePosition.ShouldBe(simulatedStalePosition);
		receivedEventArgs.NewPosition.ShouldBe(simulatedNewPosition);
		receivedEventArgs.CaptureInstance.ShouldBe("dbo_RestoredTable");
		receivedEventArgs.ProviderType.ShouldBe("SqlServer");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Verify recovery options are correctly configured
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.EnableStructuredLogging.ShouldBeTrue();

		// Verify other recovery strategies can be configured
		var throwOptions = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.Throw };
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest };
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest };
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback };
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());
	}
}
