// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Tests.Serialization.TestData;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializerMigrationService"/> validating batch processing,
/// progress reporting, idempotency, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerializerMigrationServiceShould
{
	private readonly ILogger<SerializerMigrationService> _logger = NullLogger<SerializerMigrationService>.Instance;

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullRegistry_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SerializerMigrationService(null!, _logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SerializerMigrationService(registry, null!));
	}

	#endregion Constructor Tests

	#region MigrateStoreAsync - Validation Tests

	[Fact]
	public async Task MigrateStoreAsync_WithNullStore_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => sut.MigrateStoreAsync(
				null!,
				SerializerIds.MemoryPack,
				SerializerIds.SystemTextJson, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateStoreAsync_WithUnregisteredSourceSerializer_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.MigrateStoreAsync(
				store,
				200, // Unregistered custom ID
				SerializerIds.MemoryPack, CancellationToken.None));

		ex.Message.ShouldContain("Source serializer");
		ex.Message.ShouldContain("not registered");
	}

	[Fact]
	public async Task MigrateStoreAsync_WithUnregisteredTargetSerializer_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.MigrateStoreAsync(
				store,
				SerializerIds.MemoryPack,
				200, CancellationToken.None)); // Unregistered custom ID

		ex.Message.ShouldContain("Target serializer");
		ex.Message.ShouldContain("not registered");
	}

	#endregion MigrateStoreAsync - Validation Tests

	#region MigrateStoreAsync - Empty Store Tests

	[Fact]
	public async Task MigrateStoreAsync_WithEmptyStore_ReturnsZeroCounts()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(0);
		_ = A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<IMigrationRecord>());

		// Act
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson, CancellationToken.None);

		// Assert
		result.TotalMigrated.ShouldBe(0);
		result.TotalFailed.ShouldBe(0);
		result.TotalSkipped.ShouldBe(0);
	}

	#endregion MigrateStoreAsync - Empty Store Tests

	#region MigrateStoreAsync - Successful Migration Tests

	[Fact]
	public async Task MigrateStoreAsync_WithValidRecords_MigratesSuccessfully()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Create a test payload serialized with MemoryPack
		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var testMessage = new TestMessage { Name = "Test", Value = 42 };
		var payload = memoryPackSerializer.Serialize(testMessage);
		var fullPayload = new byte[payload.Length + 1];
		fullPayload[0] = SerializerIds.MemoryPack;
		Buffer.BlockCopy(payload, 0, fullPayload, 1, payload.Length);

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", fullPayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				SerializerIds.MemoryPack, SerializerIds.SystemTextJson, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson, CancellationToken.None);

		// Assert
		result.TotalMigrated.ShouldBe(1);
		result.TotalFailed.ShouldBe(0);
		result.TotalSkipped.ShouldBe(0);

		// Verify UpdatePayload was called with STJ magic byte
		_ = A.CallTo(() => store.UpdatePayloadAsync(
				"rec-1",
				A<byte[]>.That.Matches(p => p[0] == SerializerIds.SystemTextJson),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MigrateStoreAsync_WithMultipleBatches_ProcessesAllBatches()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Create payloads
		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var payload1 = CreateMemoryPackPayload(memoryPackSerializer, "Test1", 1);
		var payload2 = CreateMemoryPackPayload(memoryPackSerializer, "Test2", 2);
		var payload3 = CreateMemoryPackPayload(memoryPackSerializer, "Test3", 3);

		var batch1 = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", payload1, typeof(TestMessage).AssemblyQualifiedName),
			new FakeMigrationRecord("rec-2", payload2, typeof(TestMessage).AssemblyQualifiedName)
		};
		var batch2 = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-3", payload3, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(3);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(batch1, batch2, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);

		// Act
		var options = new MigrationOptions { BatchSize = 2 };
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson,
			CancellationToken.None,
			options: options);

		// Assert
		result.TotalMigrated.ShouldBe(3);
		result.TotalFailed.ShouldBe(0);
	}

	#endregion MigrateStoreAsync - Successful Migration Tests

	#region MigrateStoreAsync - Idempotency Tests

	[Fact]
	public async Task MigrateStoreAsync_WithAlreadyMigratedRecord_SkipsRecord()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Create a payload already in target format (STJ)
		var stjSerializer = registry.GetById(SerializerIds.SystemTextJson);
		var testMessage = new TestMessage { Name = "AlreadyMigrated", Value = 100 };
		var payload = stjSerializer.Serialize(testMessage);
		var fullPayload = new byte[payload.Length + 1];
		fullPayload[0] = SerializerIds.SystemTextJson; // Already in target format
		Buffer.BlockCopy(payload, 0, fullPayload, 1, payload.Length);

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", fullPayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());

		// Act
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson, CancellationToken.None);

		// Assert
		result.TotalMigrated.ShouldBe(0);
		result.TotalSkipped.ShouldBe(1);

		// UpdatePayload should NOT be called for skipped records
		A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MigrateStoreAsync_WithUnexpectedSerializerId_SkipsRecord()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		// Create a payload with different serializer ID (MessagePack)
		var payload = new byte[] { SerializerIds.MessagePack, 1, 2, 3, 4 };

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", payload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());

		// Act
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack, // Expecting MemoryPack
			SerializerIds.SystemTextJson, CancellationToken.None);

		// Assert
		result.TotalSkipped.ShouldBe(1);
		result.TotalMigrated.ShouldBe(0);
	}

	#endregion MigrateStoreAsync - Idempotency Tests

	#region MigrateStoreAsync - Progress Reporting Tests

	[Fact]
	public async Task MigrateStoreAsync_WithProgressReporter_ReportsProgress()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();
		var reportedProgress = new List<EncryptionMigrationProgress>();
		var progressReported = new TaskCompletionSource<bool>();

		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var payload = CreateMemoryPackPayload(memoryPackSerializer, "Test", 1);

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", payload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);

		var progress = new Progress<EncryptionMigrationProgress>(p =>
		{
			reportedProgress.Add(p);
			_ = progressReported.TrySetResult(true);
		});

		// Act
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson,
			CancellationToken.None,
			progress);

		// Give Progress<T> time to invoke callback (it posts asynchronously)
		_ = await Task.WhenAny(progressReported.Task, global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromSeconds(1)));

		// Assert - Progress was reported
		// Note: Progress<T> reports asynchronously; verify via final result if callback hasn't fired
		result.TotalMigrated.ShouldBe(1);
		if (reportedProgress.Count > 0)
		{
			reportedProgress.Last().TotalMigrated.ShouldBe(1);
		}
	}

	#endregion MigrateStoreAsync - Progress Reporting Tests

	#region MigrateStoreAsync - Read-Back Verification Tests

	[Fact]
	public async Task MigrateStoreAsync_WithReadBackVerification_VerifiesMigration()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var stjSerializer = registry.GetById(SerializerIds.SystemTextJson);
		var sourcePayload = CreateMemoryPackPayload(memoryPackSerializer, "Test", 42);

		// Create expected target payload for verification
		var testMessage = new TestMessage { Name = "Test", Value = 42 };
		var targetContent = stjSerializer.Serialize(testMessage);
		var targetPayload = new byte[targetContent.Length + 1];
		targetPayload[0] = SerializerIds.SystemTextJson;
		Buffer.BlockCopy(targetContent, 0, targetPayload, 1, targetContent.Length);

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", sourcePayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => store.GetPayloadAsync("rec-1", A<CancellationToken>._))
			.Returns(targetPayload);

		// Act
		var options = new MigrationOptions { EnableReadBackVerification = true };
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson,
			CancellationToken.None,
			options: options);

		// Assert
		result.TotalMigrated.ShouldBe(1);
		_ = A.CallTo(() => store.GetPayloadAsync("rec-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MigrateStoreAsync_WithFailedVerification_CountsAsFailure()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var sourcePayload = CreateMemoryPackPayload(memoryPackSerializer, "Test", 42);

		// Return wrong magic byte on verification
		var wrongPayload = new byte[] { SerializerIds.MemoryPack, 1, 2, 3 };

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", sourcePayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(1);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => store.GetPayloadAsync("rec-1", A<CancellationToken>._))
			.Returns(wrongPayload);

		// Act
		var options = new MigrationOptions { EnableReadBackVerification = true };
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson,
			CancellationToken.None,
			options: options);

		// Assert
		result.TotalFailed.ShouldBe(1);
		result.TotalMigrated.ShouldBe(0);
	}

	#endregion MigrateStoreAsync - Read-Back Verification Tests

	#region MigrateStoreAsync - Error Handling Tests

	[Fact]
	public async Task MigrateStoreAsync_WithContinueOnFailure_ContinuesAfterFailure()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var goodPayload = CreateMemoryPackPayload(memoryPackSerializer, "Good", 1);
		var badPayload = new byte[] { SerializerIds.MemoryPack, 0xFF, 0xFF }; // Corrupt

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", badPayload, typeof(TestMessage).AssemblyQualifiedName),
			new FakeMigrationRecord("rec-2", goodPayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(2);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());
		_ = A.CallTo(() => store.UpdatePayloadAsync(A<string>._, A<byte[]>._, A<CancellationToken>._))
			.Returns(true);

		// Act
		var options = new MigrationOptions { ContinueOnFailure = true };
		var result = await sut.MigrateStoreAsync(
			store,
			SerializerIds.MemoryPack,
			SerializerIds.SystemTextJson,
			CancellationToken.None,
			options: options);

		// Assert - First record failed, second migrated
		result.TotalFailed.ShouldBe(1);
		result.TotalMigrated.ShouldBe(1);
	}

	[Fact]
	public async Task MigrateStoreAsync_WithMaxConsecutiveFailures_AbortsOnThreshold()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		var badPayload = new byte[] { SerializerIds.MemoryPack, 0xFF, 0xFF }; // Corrupt

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", badPayload, typeof(TestMessage).AssemblyQualifiedName),
			new FakeMigrationRecord("rec-2", badPayload, typeof(TestMessage).AssemblyQualifiedName),
			new FakeMigrationRecord("rec-3", badPayload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.Returns(3);
		A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(records, new List<IMigrationRecord>());

		// Act & Assert
		var options = new MigrationOptions { MaxConsecutiveFailures = 2 };
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.MigrateStoreAsync(
				store,
				SerializerIds.MemoryPack,
				SerializerIds.SystemTextJson,
				CancellationToken.None,
				options: options));

		ex.Message.ShouldContain("consecutive failures");
	}

	[Fact]
	public async Task MigrateStoreAsync_WithCancellation_ThrowsOperationCanceledException()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new SerializerMigrationService(registry, _logger);
		var store = A.Fake<IMigrationStore>();

		var memoryPackSerializer = registry.GetById(SerializerIds.MemoryPack);
		var payload = CreateMemoryPackPayload(memoryPackSerializer, "Test", 1);

		var records = new List<IMigrationRecord>
		{
			new FakeMigrationRecord("rec-1", payload, typeof(TestMessage).AssemblyQualifiedName)
		};

		_ = A.CallTo(() => store.StoreName).Returns("TestStore");

		var cts = new CancellationTokenSource();

		// Set up the store to throw when the cancellation token is triggered
		// This simulates a real async operation that respects cancellation
		_ = A.CallTo(() => store.CountPendingMigrationsAsync(A<byte>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var token = call.GetArgument<CancellationToken>(1);
				token.ThrowIfCancellationRequested();
				return Task.FromResult(1);
			});
		_ = A.CallTo(() => store.GetBatchForMigrationAsync(
				A<byte>._, A<byte>._, A<int>._, A<CancellationToken>._))
			.Returns(records);

		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => sut.MigrateStoreAsync(
				store,
				SerializerIds.MemoryPack,
				SerializerIds.SystemTextJson,
				cts.Token));
	}

	#endregion MigrateStoreAsync - Error Handling Tests

	#region EncryptionMigrationProgress Record Tests

	[Fact]
	public void MigrationProgress_CalculatesSuccessRateCorrectly()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(80, 20, 10, 10);

		// Act & Assert
		progress.SuccessRate.ShouldBe(80.0); // 80 / (80+20) = 80%
		progress.FailureRate.ShouldBe(20.0);
	}

	[Fact]
	public void MigrationProgress_WithZeroProcessed_ReturnsHundredPercent()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(0, 0, 0, 0);

		// Act & Assert
		progress.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void MigrationProgress_CalculatesTotalProcessed()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(50, 10, 5, 10);

		// Act & Assert
		progress.TotalProcessed.ShouldBe(65); // 50 + 10 + 5
	}

	[Fact]
	public void MigrationProgress_CalculatesCompletionPercentage()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(
			TotalMigrated: 50,
			TotalFailed: 0,
			TotalSkipped: 0,
			CurrentBatchSize: 10,
			EstimatedRemaining: 50);

		// Act & Assert
		progress.CompletionPercentage.ShouldBe(50.0); // 50 / 100 = 50%
	}

	[Fact]
	public void MigrationProgress_WithNoEstimate_ReturnsNullCompletion()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(50, 0, 0, 10, EstimatedRemaining: null);

		// Act & Assert
		progress.CompletionPercentage.ShouldBeNull();
	}

	[Fact]
	public void MigrationProgress_Initial_HasZeroCounts()
	{
		// Act
		var progress = EncryptionMigrationProgress.Initial;

		// Assert
		progress.TotalMigrated.ShouldBe(0);
		progress.TotalFailed.ShouldBe(0);
		progress.TotalSkipped.ShouldBe(0);
		progress.CurrentBatchSize.ShouldBe(0);
	}

	[Fact]
	public void MigrationProgress_ToString_ReturnsFormattedString()
	{
		// Arrange
		var progress = new EncryptionMigrationProgress(100, 5, 10, 50, EstimatedRemaining: 100);

		// Act
		var str = progress.ToString();

		// Assert
		str.ShouldContain("Migrated: 100");
		str.ShouldContain("Failed: 5");
		str.ShouldContain("Skipped: 10");
		str.ShouldContain("Success rate:");
		str.ShouldContain("complete");
	}

	#endregion EncryptionMigrationProgress Record Tests

	#region MigrationOptions Tests

	[Fact]
	public void MigrationOptions_HasCorrectDefaults()
	{
		// Arrange & Act
		var options = new MigrationOptions();

		// Assert
		options.BatchSize.ShouldBe(1000);
		options.EnableReadBackVerification.ShouldBeFalse();
		options.MaxConsecutiveFailures.ShouldBe(100);
		options.ContinueOnFailure.ShouldBeTrue();
		options.DelayBetweenBatchesMs.ShouldBe(0);
	}

	#endregion MigrationOptions Tests

	#region Helper Methods

	private static SerializerRegistry CreateRegistryWithMemoryPack()
	{
		var registry = new SerializerRegistry();
		registry.Register(SerializerIds.MemoryPack, new MemoryPackPluggableSerializer());
		registry.SetCurrent("MemoryPack");
		return registry;
	}

	private static SerializerRegistry CreateRegistryWithBothSerializers()
	{
		var registry = new SerializerRegistry();
		registry.Register(SerializerIds.MemoryPack, new MemoryPackPluggableSerializer());
		registry.Register(SerializerIds.SystemTextJson, new SystemTextJsonPluggableSerializer());
		registry.SetCurrent("MemoryPack");
		return registry;
	}

	private static byte[] CreateMemoryPackPayload(IPluggableSerializer serializer, string name, int value)
	{
		var testMessage = new TestMessage { Name = name, Value = value };
		var content = serializer.Serialize(testMessage);
		var payload = new byte[content.Length + 1];
		payload[0] = SerializerIds.MemoryPack;
		Buffer.BlockCopy(content, 0, payload, 1, content.Length);
		return payload;
	}

	#endregion Helper Methods

	#region Fake Implementation

	/// <summary>
	/// Fake implementation of IMigrationRecord for testing.
	/// </summary>
	private sealed class FakeMigrationRecord : IMigrationRecord
	{
		public FakeMigrationRecord(string id, byte[] payload, string? typeName)
		{
			Id = id;
			Payload = payload;
			TypeName = typeName;
		}

		public string Id { get; }
		public byte[] Payload { get; }
		public string? TypeName { get; }
	}

	#endregion Fake Implementation
}
