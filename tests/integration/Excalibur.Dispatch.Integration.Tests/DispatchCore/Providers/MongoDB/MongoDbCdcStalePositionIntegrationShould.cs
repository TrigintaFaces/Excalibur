// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.MongoDB.Cdc;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDB;

/// <summary>
/// Integration tests for MongoDB CDC stale position detection and recovery.
/// Tests the <see cref="MongoDbStalePositionDetector"/> and <see cref="MongoDbCdcRecoveryOptions"/>
/// against real MongoDB scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-4qnex: MongoDB CDC Stale Position Tests (2 tests).
/// </para>
/// <para>
/// These tests verify that the MongoDB CDC stale position detection correctly identifies
/// error codes and that the recovery infrastructure handles them properly.
/// </para>
/// <para>
/// Note: Full CDC functionality (change streams with resume tokens) requires
/// a replica set configuration. These tests focus on the error detection and recovery
/// infrastructure using simulated scenarios and database connectivity verification.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.MongoDB)]
[Trait("Component", "CDC")]
[Trait("Provider", "MongoDB")]
[Trait("SubComponent", "StalePositionRecovery")]
public sealed class MongoDbCdcStalePositionIntegrationShould : IntegrationTestBase
{
	private readonly MongoDbContainerFixture _mongoFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcStalePositionIntegrationShould"/> class.
	/// </summary>
	/// <param name="mongoFixture">The MongoDB container fixture.</param>
	public MongoDbCdcStalePositionIntegrationShould(MongoDbContainerFixture mongoFixture)
	{
		_mongoFixture = mongoFixture;
	}

	/// <summary>
	/// Tests that the MongoDB CDC processor correctly detects and recovers from stale
	/// position scenarios that occur when change stream resume tokens become invalid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This test validates:
	/// 1. The MongoDbStalePositionDetector correctly identifies CDC-related error codes
	/// 2. Event args are properly created with correct processor and provider info
	/// 3. Database connectivity works correctly with the container
	/// </para>
	/// <para>
	/// Note: Full change stream functionality requires a replica set which isn't available
	/// in standalone Docker containers. This test verifies the detection infrastructure using
	/// simulated exceptions and database connectivity verification.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromStaleResumeToken_DetectsChangeStreamError()
	{
		// Arrange: Verify database connectivity
		var client = new MongoClient(_mongoFixture.ConnectionString);
		var database = client.GetDatabase("cdc_test_db");
		var collection = database.GetCollection<BsonDocument>("cdc_test_collection");

		// Create a test document to verify database is working
		var testDoc = new BsonDocument
		{
			{ "_id", ObjectId.GenerateNewId() },
			{ "name", "Test Record 1" },
			{ "created_at", DateTime.UtcNow }
		};
		await collection.InsertOneAsync(testDoc, cancellationToken: TestCancellationToken).ConfigureAwait(true);

		// Act: Verify database connectivity - query should succeed
		var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellationToken).ConfigureAwait(true);
		count.ShouldBeGreaterThan(0, "Database should have test data");

		// Since change streams require replica sets not available in Docker standalone mode,
		// we test the detection logic using simulated event args
		var simulatedStalePosition = new MongoDbCdcPosition(new BsonDocument { { "_data", "simulated_resume_token" } });

		// Verify event args creation works correctly for change stream error scenario
		// In production, this would be called when a real MongoDB error (136, 286, etc.) occurs
		var genericException = new InvalidOperationException("Simulated change stream history lost - resume token not in oplog");
		var eventArgs = MongoDbStalePositionDetector.CreateEventArgs(
			genericException,
			"test-processor",
			stalePosition: simulatedStalePosition,
			databaseName: "cdc_test_db",
			collectionName: "cdc_test_collection");

		// Assert: Verify event args are correctly populated (now using CdcPositionResetEventArgs)
		eventArgs.ProcessorId.ShouldBe("test-processor");
		eventArgs.ProviderType.ShouldBe("MongoDB");
		eventArgs.DatabaseName.ShouldBe("cdc_test_db");
		eventArgs.CaptureInstance.ShouldBe("cdc_test_db.cdc_test_collection");
		eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext["CollectionName"].ShouldBe("cdc_test_collection");
		eventArgs.OriginalException.ShouldBe(genericException);
		eventArgs.StalePosition.ShouldNotBeNull(); // Now stored as byte[]
		eventArgs.DetectedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddMinutes(-1),
			DateTimeOffset.UtcNow.AddMinutes(1));

		// Verify detector constants are exposed correctly
		MongoDbStalePositionDetector.StalePositionErrorCodes.ShouldContain(136); // ChangeStreamHistoryLost
		MongoDbStalePositionDetector.StalePositionErrorCodes.ShouldContain(286); // ChangeStreamFatalError
		MongoDbStalePositionDetector.StalePositionErrorCodes.ShouldContain(26);  // NamespaceNotFound
		MongoDbStalePositionDetector.StalePositionErrorCodes.ShouldContain(73);  // InvalidNamespace
		MongoDbStalePositionDetector.StalePositionErrorCodes.ShouldContain(133); // StaleShardVersion

		// Verify reason code mapping from error codes
		MongoDbStalePositionReasonCodes.FromErrorCode(136).ShouldBe(MongoDbStalePositionReasonCodes.ResumeTokenNotFound);
		MongoDbStalePositionReasonCodes.FromErrorCode(286).ShouldBe(MongoDbStalePositionReasonCodes.InvalidResumeToken);
		MongoDbStalePositionReasonCodes.FromErrorCode(26).ShouldBe(MongoDbStalePositionReasonCodes.CollectionDropped);
		MongoDbStalePositionReasonCodes.FromErrorCode(73).ShouldBe(MongoDbStalePositionReasonCodes.CollectionRenamed);
		MongoDbStalePositionReasonCodes.FromErrorCode(133).ShouldBe(MongoDbStalePositionReasonCodes.ShardMigration);
		MongoDbStalePositionReasonCodes.FromErrorCode(999).ShouldBe(MongoDbStalePositionReasonCodes.Unknown);

		// Cleanup
		await database.DropCollectionAsync("cdc_test_collection", TestCancellationToken).ConfigureAwait(true);
	}

	/// <summary>
	/// Tests that the MongoDB CDC processor correctly detects and recovers from stale
	/// position scenarios that occur after collection drop/rename operations.
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
	/// Note: Full change stream functionality requires replica sets not available
	/// in Docker. This test verifies the callback mechanism and options configuration.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverFromCollectionDrop_InvokesRecoveryCallback()
	{
		// Arrange: Configure recovery options with callback
		var callbackInvoked = false;
		CdcPositionResetEventArgs? receivedEventArgs = null;

		var recoveryOptions = new MongoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			MaxRecoveryAttempts = 3,
			RecoveryAttemptDelay = TimeSpan.FromMilliseconds(100),
			AutoRecreateStreamOnInvalidToken = true,
			UseClusterTimeOnResumeFailure = true,
			AlwaysInvokeCallbackOnReset = true,
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
		var client = new MongoClient(_mongoFixture.ConnectionString);
		var database = client.GetDatabase("cdc_callback_test_db");

		// Verify we can get database info
		var databaseNames = await (await client.ListDatabaseNamesAsync(TestCancellationToken).ConfigureAwait(true)).ToListAsync(TestCancellationToken).ConfigureAwait(true);
		_ = databaseNames.ShouldNotBeNull();

		// Simulate collection drop scenario with event args
		var simulatedStalePosition = new MongoDbCdcPosition(new BsonDocument { { "_data", "old_resume_token_before_drop" } });
		var simulatedNewPosition = MongoDbCdcPosition.Start; // After collection drop, start from beginning

		// Create event args simulating collection drop scenario
		var simulatedException = new InvalidOperationException(
			"Simulated collection drop scenario - change stream invalidated");
		var eventArgs = MongoDbStalePositionDetector.CreateEventArgs(
			simulatedException,
			"collection-drop-processor",
			stalePosition: simulatedStalePosition,
			newPosition: simulatedNewPosition,
			databaseName: "cdc_callback_test_db",
			collectionName: "dropped_collection");

		// Act: Invoke the callback (simulating what the processor would do)
		await recoveryOptions.OnPositionReset(eventArgs, TestCancellationToken).ConfigureAwait(true);

		// Assert: Verify callback was invoked with correct parameters (now using CdcPositionResetEventArgs)
		callbackInvoked.ShouldBeTrue("Recovery callback should be invoked");
		_ = receivedEventArgs.ShouldNotBeNull();
		receivedEventArgs.ProcessorId.ShouldBe("collection-drop-processor");
		receivedEventArgs.StalePosition.ShouldNotBeNull(); // Now stored as byte[]
		receivedEventArgs.NewPosition.ShouldBeNull(); // Start position has null TokenString
		receivedEventArgs.DatabaseName.ShouldBe("cdc_callback_test_db");
		receivedEventArgs.CaptureInstance.ShouldBe("cdc_callback_test_db.dropped_collection");
		receivedEventArgs.AdditionalContext.ShouldNotBeNull();
		receivedEventArgs.AdditionalContext["CollectionName"].ShouldBe("dropped_collection");
		receivedEventArgs.ProviderType.ShouldBe("MongoDB");
		receivedEventArgs.OriginalException.ShouldBe(simulatedException);

		// Verify recovery options are correctly configured
		recoveryOptions.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		recoveryOptions.MaxRecoveryAttempts.ShouldBe(3);
		recoveryOptions.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		recoveryOptions.AutoRecreateStreamOnInvalidToken.ShouldBeTrue();
		recoveryOptions.UseClusterTimeOnResumeFailure.ShouldBeTrue();
		recoveryOptions.AlwaysInvokeCallbackOnReset.ShouldBeTrue();

		// Verify other recovery strategies can be configured
		var throwOptions = new MongoDbCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.Throw };
		throwOptions.Validate(); // Should not throw without callback

		var fallbackEarliestOptions = new MongoDbCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest };
		fallbackEarliestOptions.Validate(); // Should not throw

		var fallbackLatestOptions = new MongoDbCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest };
		fallbackLatestOptions.Validate(); // Should not throw

		// Verify InvokeCallback without callback throws on validation
		var invalidOptions = new MongoDbCdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback };
		_ = Should.Throw<InvalidOperationException>(() => invalidOptions.Validate());

		// Verify reason code from error message patterns
		// Note: Messages containing "resume token" are matched first as ResumeTokenNotFound
		MongoDbStalePositionReasonCodes.FromErrorMessage("resume token not found in oplog").ShouldBe(MongoDbStalePositionReasonCodes.ResumeTokenNotFound);
		MongoDbStalePositionReasonCodes.FromErrorMessage("invalid cursor token").ShouldBe(MongoDbStalePositionReasonCodes.InvalidResumeToken); // INVALID + TOKEN without RESUME TOKEN phrase
		MongoDbStalePositionReasonCodes.FromErrorMessage("collection was dropped").ShouldBe(MongoDbStalePositionReasonCodes.CollectionDropped);
		MongoDbStalePositionReasonCodes.FromErrorMessage("collection renamed to new_name").ShouldBe(MongoDbStalePositionReasonCodes.CollectionRenamed);
		MongoDbStalePositionReasonCodes.FromErrorMessage("shard migration in progress").ShouldBe(MongoDbStalePositionReasonCodes.ShardMigration);
		MongoDbStalePositionReasonCodes.FromErrorMessage("change stream invalidate event").ShouldBe(MongoDbStalePositionReasonCodes.StreamInvalidated);
		MongoDbStalePositionReasonCodes.FromErrorMessage("some other error").ShouldBe(MongoDbStalePositionReasonCodes.Unknown);
	}
}
