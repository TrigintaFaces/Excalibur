// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionMigrationService"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EncryptionMigrationServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly EncryptionMigrationService _sut;

	public EncryptionMigrationServiceShould()
	{
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_sut = new EncryptionMigrationService(
			_encryptionProvider,
			NullLogger<EncryptionMigrationService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionMigrationService(
			null!,
			NullLogger<EncryptionMigrationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionMigrationService(
			A.Fake<IEncryptionProvider>(),
			null!));
	}

	#endregion Constructor Tests

	#region MigrateAsync Tests

	[Fact]
	public async Task MigrateAsync_ThrowArgumentNullException_WhenEncryptedDataIsNull()
	{
		// Arrange
		var sourceContext = CreateEncryptionContext();
		var targetContext = CreateEncryptionContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateAsync(null!, sourceContext, targetContext, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateAsync_ThrowArgumentNullException_WhenSourceContextIsNull()
	{
		// Arrange
		var data = CreateEncryptedData();
		var targetContext = CreateEncryptionContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateAsync(data, null!, targetContext, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateAsync_ThrowArgumentNullException_WhenTargetContextIsNull()
	{
		// Arrange
		var data = CreateEncryptedData();
		var sourceContext = CreateEncryptionContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateAsync(data, sourceContext, null!, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateAsync_ReturnSuccessResult_WhenMigrationSucceeds()
	{
		// Arrange
		var originalData = CreateEncryptedData("original-key");
		var migratedData = CreateEncryptedData("migrated-key");
		var sourceContext = CreateEncryptionContext();
		var targetContext = CreateEncryptionContext();
		var decryptedBytes = new byte[] { 1, 2, 3, 4 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(originalData, sourceContext, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(migratedData);

		// Act
		var result = await _sut.MigrateAsync(originalData, sourceContext, targetContext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.MigratedData.ShouldBe(migratedData);
		result.SourceKeyId.ShouldBe("original-key");
		result.TargetKeyId.ShouldBe("migrated-key");
		result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task MigrateAsync_ReturnFailedResult_WhenDecryptionFails()
	{
		// Arrange
		var data = CreateEncryptedData();
		var sourceContext = CreateEncryptionContext();
		var targetContext = CreateEncryptionContext();

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(data, sourceContext, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Decryption failed"));

		// Act
		var result = await _sut.MigrateAsync(data, sourceContext, targetContext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.MigratedData.ShouldBeNull();
		result.ErrorMessage.ShouldContain("Decryption failed");
		_ = result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public async Task MigrateAsync_ReturnFailedResult_WhenEncryptionFails()
	{
		// Arrange
		var data = CreateEncryptedData();
		var sourceContext = CreateEncryptionContext();
		var targetContext = CreateEncryptionContext();
		var decryptedBytes = new byte[] { 1, 2, 3, 4 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(data, sourceContext, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Encryption failed"));

		// Act
		var result = await _sut.MigrateAsync(data, sourceContext, targetContext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Encryption failed");
	}

	[Fact]
	public async Task MigrateAsync_CallDecryptThenEncrypt()
	{
		// Arrange
		var data = CreateEncryptedData();
		var sourceContext = CreateEncryptionContext();
		var targetContext = CreateEncryptionContext();
		var decryptedBytes = new byte[] { 1, 2, 3, 4 };
		var migratedData = CreateEncryptedData("new-key");

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(data, sourceContext, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(migratedData);

		// Act
		_ = await _sut.MigrateAsync(data, sourceContext, targetContext, CancellationToken.None);

		// Assert - verify call order
		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(data, sourceContext, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly());
	}

	#endregion MigrateAsync Tests

	#region MigrateBatchAsync Tests

	[Fact]
	public async Task MigrateBatchAsync_ThrowArgumentNullException_WhenItemsIsNull()
	{
		// Arrange
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateBatchAsync(null!, targetContext, options, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateBatchAsync_ThrowArgumentNullException_WhenTargetContextIsNull()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>();
		var options = new BatchMigrationOptions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateBatchAsync(items, null!, options, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateBatchAsync_ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>();
		var targetContext = CreateEncryptionContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MigrateBatchAsync(items, targetContext, null!, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateBatchAsync_ReturnEmptyResult_WhenNoItems()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>();
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions();

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.TotalItems.ShouldBe(0);
		result.SucceededCount.ShouldBe(0);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task MigrateBatchAsync_MigrateAllItems_WhenAllSucceed()
	{
		// Arrange
		var item1 = CreateMigrationItem("item-1", "key-1");
		var item2 = CreateMigrationItem("item-2", "key-2");
		var items = new List<EncryptionMigrationItem> { item1, item2 };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { MaxDegreeOfParallelism = 1 };
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.TotalItems.ShouldBe(2);
		result.SucceededCount.ShouldBe(2);
		result.FailedCount.ShouldBe(0);
		_ = result.SuccessResults.ShouldNotBeNull();
		result.SuccessResults.Count.ShouldBe(2);
	}

	[Fact]
	public async Task MigrateBatchAsync_ContinueOnError_WhenOptionSet()
	{
		// Arrange
		var item1 = CreateMigrationItem("item-1", "key-1");
		var item2 = CreateMigrationItem("item-2", "key-2");
		var items = new List<EncryptionMigrationItem> { item1, item2 };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { ContinueOnError = true, MaxDegreeOfParallelism = 1 };
		var decryptedBytes = new byte[] { 1, 2, 3 };

		// First item fails, second succeeds
		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(item1.EncryptedData, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Decryption failed"));
		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(item2.EncryptedData, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse(); // Overall failed because one item failed
		result.TotalItems.ShouldBe(2);
		result.SucceededCount.ShouldBe(1);
		result.FailedCount.ShouldBe(1);
		result.IsPartialSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task MigrateBatchAsync_UseItemTimeout_WhenSpecified()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions
		{
			ItemTimeout = TimeSpan.FromMinutes(5), // Set a timeout
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.SucceededCount.ShouldBe(1);
	}

	[Fact]
	public async Task MigrateBatchAsync_UseZeroItemTimeout_WhenNotSpecified()
	{
		// Arrange - ItemTimeout is zero (default), meaning no per-item timeout
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions
		{
			ItemTimeout = TimeSpan.Zero, // No per-item timeout
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task MigrateBatchAsync_UseMigrationId_FromOptions()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { MigrationId = "custom-migration-id", MaxDegreeOfParallelism = 1 };
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.MigrationId.ShouldBe("custom-migration-id");
	}

	[Fact]
	public async Task MigrateBatchAsync_GenerateMigrationId_WhenNotProvided()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { MaxDegreeOfParallelism = 1 };
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.MigrationId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(result.MigrationId, out _).ShouldBeTrue();
	}

	[Fact]
	public async Task MigrateBatchAsync_ReportProgress_WhenTrackProgressEnabled()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>
		{
			CreateMigrationItem("item-1", "key-1"),
			CreateMigrationItem("item-2", "key-2")
		};
		var targetContext = CreateEncryptionContext();
		var progressReports = new List<EncryptionMigrationProgress>();
		var progress = new Progress<EncryptionMigrationProgress>(p => progressReports.Add(p));
		var options = new BatchMigrationOptions
		{
			TrackProgress = true,
			Progress = progress,
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		_ = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert - give time for progress to be reported
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		progressReports.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task MigrateBatchAsync_ReportProgressWithCorrectProperties()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>
		{
			CreateMigrationItem("item-1", "key-1"),
			CreateMigrationItem("item-2", "key-2")
		};
		var targetContext = CreateEncryptionContext();
		var progressReports = new List<EncryptionMigrationProgress>();
		var progress = new Progress<EncryptionMigrationProgress>(p =>
		{
			// Clone the progress to avoid race conditions
			progressReports.Add(new EncryptionMigrationProgress
			{
				TotalItems = p.TotalItems,
				CompletedItems = p.CompletedItems,
				SucceededItems = p.SucceededItems,
				FailedItems = p.FailedItems,
				CurrentItemId = p.CurrentItemId,
				Elapsed = p.Elapsed,
				EstimatedRemaining = p.EstimatedRemaining
			});
		});
		var options = new BatchMigrationOptions
		{
			TrackProgress = true,
			Progress = progress,
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		_ = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert - give time for progress to be reported
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		progressReports.Count.ShouldBeGreaterThanOrEqualTo(1);

		// Verify final progress report has correct values
		var lastReport = progressReports[progressReports.Count - 1];
		lastReport.TotalItems.ShouldBe(2);
		lastReport.CompletedItems.ShouldBeGreaterThanOrEqualTo(1);
		lastReport.SucceededItems.ShouldBeGreaterThanOrEqualTo(1);
		lastReport.FailedItems.ShouldBe(0);
		lastReport.CurrentItemId.ShouldNotBeNullOrEmpty();
		lastReport.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task MigrateBatchAsync_DoNotReportProgress_WhenTrackProgressDisabled()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var progressReports = new List<EncryptionMigrationProgress>();
		var progress = new Progress<EncryptionMigrationProgress>(p => progressReports.Add(p));
		var options = new BatchMigrationOptions
		{
			TrackProgress = false, // Disabled
			Progress = progress,
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		_ = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert - give time for progress (which shouldn't be reported)
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		progressReports.ShouldBeEmpty();
	}

	[Fact]
	public async Task MigrateBatchAsync_DoNotReportProgress_WhenProgressIsNull()
	{
		// Arrange - Progress is null, but TrackProgress is true
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions
		{
			TrackProgress = true,
			Progress = null, // Null progress handler
			MaxDegreeOfParallelism = 1
		};
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act - Should not throw even with null progress
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task MigrateBatchAsync_HandleCancellation()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem>
		{
			CreateMigrationItem("item-1", "key-1"),
			CreateMigrationItem("item-2", "key-2")
		};
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { MaxDegreeOfParallelism = 1 };
		using var cts = new CancellationTokenSource();

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes(() => cts.Cancel())
			.ThrowsAsync(new OperationCanceledException());

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, cts.Token);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task MigrateBatchAsync_ThrowEncryptionMigrationException_WhenContinueOnErrorIsFalse()
	{
		// Arrange
		var item1 = CreateMigrationItem("item-1", "key-1");
		var items = new List<EncryptionMigrationItem> { item1 };
		var targetContext = CreateEncryptionContext();
		var options = new BatchMigrationOptions { ContinueOnError = false, MaxDegreeOfParallelism = 1 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(item1.EncryptedData, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Decryption failed"));

		// Act & Assert
		var ex = await Should.ThrowAsync<EncryptionMigrationException>(() =>
			_sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None));

		ex.ItemId.ShouldBe("item-1");
		ex.MigrationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task MigrateBatchAsync_SetStatusToCancelled_WhenOperationCancelled()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var migrationId = "cancellation-test-" + Guid.NewGuid();
		var options = new BatchMigrationOptions
		{
			MigrationId = migrationId,
			MaxDegreeOfParallelism = 1
		};
		using var cts = new CancellationTokenSource();

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes(() => cts.Cancel())
			.ThrowsAsync(new OperationCanceledException());

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, cts.Token);

		// Assert - Verify result and status are set correctly
		result.Success.ShouldBeFalse();
		result.MigrationId.ShouldBe(migrationId);
		result.CompletedAt.ShouldNotBe(default);

		// Also verify internal status was updated
		var status = await _sut.GetMigrationStatusAsync(migrationId, CancellationToken.None);
		_ = status.ShouldNotBeNull();
		status.State.ShouldBe(MigrationState.Cancelled);
		status.ErrorMessage.ShouldBe("Migration was cancelled");
	}

	[Fact]
	public async Task MigrateBatchAsync_SetStatusToFailed_WhenItemsFail()
	{
		// Arrange
		var item1 = CreateMigrationItem("item-1", "key-1");
		var items = new List<EncryptionMigrationItem> { item1 };
		var targetContext = CreateEncryptionContext();
		var migrationId = "failed-test-" + Guid.NewGuid();
		var options = new BatchMigrationOptions
		{
			MigrationId = migrationId,
			ContinueOnError = true,
			MaxDegreeOfParallelism = 1
		};

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(item1.EncryptedData, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Decryption failed"));

		// Act
		var result = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.FailedCount.ShouldBe(1);

		var status = await _sut.GetMigrationStatusAsync(migrationId, CancellationToken.None);
		_ = status.ShouldNotBeNull();
		status.State.ShouldBe(MigrationState.Failed);
		status.FailedItems.ShouldBe(1);
	}

	#endregion MigrateBatchAsync Tests

	#region RequiresMigrationAsync Tests

	[Fact]
	public async Task RequiresMigrationAsync_ThrowArgumentNullException_WhenDataIsNull()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RequiresMigrationAsync(null!, policy, CancellationToken.None));
	}

	[Fact]
	public async Task RequiresMigrationAsync_ThrowArgumentNullException_WhenPolicyIsNull()
	{
		// Arrange
		var data = CreateEncryptedData();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RequiresMigrationAsync(data, null!, CancellationToken.None));
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnTrue_WhenKeyIdIsDeprecated()
	{
		// Arrange
		var data = CreateEncryptedData("deprecated-key");
		var policy = MigrationPolicy.ForDeprecatedKeys("deprecated-key");

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnTrue_WhenAlgorithmIsDeprecated()
	{
		// Arrange
		var data = CreateEncryptedData(algorithm: EncryptionAlgorithm.Aes256CbcHmac);
		var policy = new MigrationPolicy
		{
			DeprecatedAlgorithms = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac }
		};

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnTrue_WhenTargetAlgorithmDiffers()
	{
		// Arrange
		var data = CreateEncryptedData(algorithm: EncryptionAlgorithm.Aes256CbcHmac);
		var policy = MigrationPolicy.ForAlgorithm(EncryptionAlgorithm.Aes256Gcm);

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnTrue_WhenKeyAgeExceedsMax()
	{
		// Arrange
		var data = new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3 },
			KeyId = "test-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[12],
			EncryptedAt = DateTimeOffset.UtcNow.AddDays(-100)
		};
		var policy = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(90) };

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnTrue_WhenEncryptedBeforeThreshold()
	{
		// Arrange
		var encryptedAt = DateTimeOffset.UtcNow.AddDays(-10);
		var data = new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3 },
			KeyId = "test-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[12],
			EncryptedAt = encryptedAt
		};
		var policy = new MigrationPolicy { EncryptedBefore = DateTimeOffset.UtcNow.AddDays(-5) };

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnFalse_WhenTenantNotInScope()
	{
		// Arrange - data has deprecated key but is not in tenant scope
		var data = new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3 },
			KeyId = "deprecated-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[12],
			TenantId = "tenant-b"
		};
		var policy = new MigrationPolicy
		{
			DeprecatedKeyIds = new HashSet<string> { "deprecated-key" },
			TenantIds = new HashSet<string> { "tenant-a" } // Only tenant-a is in scope
		};

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RequiresMigrationAsync_ReturnFalse_WhenNoConditionsMet()
	{
		// Arrange
		var data = CreateEncryptedData();
		var policy = new MigrationPolicy
		{
			DeprecatedKeyIds = new HashSet<string> { "other-key" },
			TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm // Same as data
		};

		// Act
		var result = await _sut.RequiresMigrationAsync(data, policy, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion RequiresMigrationAsync Tests

	#region GetMigrationStatusAsync Tests

	[Fact]
	public async Task GetMigrationStatusAsync_ThrowArgumentNullException_WhenMigrationIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.GetMigrationStatusAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetMigrationStatusAsync_ReturnNull_WhenMigrationNotFound()
	{
		// Act
		var result = await _sut.GetMigrationStatusAsync("non-existent-migration", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetMigrationStatusAsync_ReturnStatus_AfterBatchMigration()
	{
		// Arrange
		var items = new List<EncryptionMigrationItem> { CreateMigrationItem("item-1", "key-1") };
		var targetContext = CreateEncryptionContext();
		var migrationId = "test-migration-" + Guid.NewGuid();
		var options = new BatchMigrationOptions { MigrationId = migrationId, MaxDegreeOfParallelism = 1 };
		var decryptedBytes = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(decryptedBytes);
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(decryptedBytes, targetContext, A<CancellationToken>._))
			.Returns(CreateEncryptedData("new-key"));

		// Act
		_ = await _sut.MigrateBatchAsync(items, targetContext, options, CancellationToken.None);
		var result = await _sut.GetMigrationStatusAsync(migrationId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.MigrationId.ShouldBe(migrationId);
		result.State.ShouldBe(MigrationState.Completed);
		result.TotalItems.ShouldBe(1);
		result.SucceededItems.ShouldBe(1);
	}

	#endregion GetMigrationStatusAsync Tests

	#region EstimateMigrationAsync Tests

	[Fact]
	public async Task EstimateMigrationAsync_ThrowArgumentNullException_WhenPolicyIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.EstimateMigrationAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateMigrationAsync_ReturnGuidanceEstimate_WhenNoDataProvided()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.EstimatedItemCount.ShouldBe(0);
		result.EstimatedDataSizeBytes.ShouldBe(0);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task EstimateMigrationAsync_ReturnWarning_GuidingToTypedOverload()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, CancellationToken.None);

		// Assert
		result.Warnings.ShouldContain(w => w.Contains("EstimateMigrationAsync"));
	}

	[Fact]
	public async Task EstimateMigrationAsync_ThrowOperationCanceledException_WhenCancelled()
	{
		// Arrange
		var policy = MigrationPolicy.Default;
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_sut.EstimateMigrationAsync(policy, cts.Token));
	}

	#endregion EstimateMigrationAsync Tests

	#region EstimateMigrationAsync (typed) Tests

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ThrowArgumentNullException_WhenPolicyIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.EstimateMigrationAsync(null!, 100, 1024, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ThrowArgumentOutOfRangeException_WhenItemCountIsNegative()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.EstimateMigrationAsync(policy, -1, 1024, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ThrowArgumentOutOfRangeException_WhenDataSizeIsNegative()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.EstimateMigrationAsync(policy, 100, -1, CancellationToken.None));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ThrowOperationCanceledException_WhenCancelled()
	{
		// Arrange
		var policy = MigrationPolicy.Default;
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_sut.EstimateMigrationAsync(policy, 100, 1024, cts.Token));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnZeroEstimate_WhenItemCountIsZero()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 0, 0, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(0);
		result.EstimatedDataSizeBytes.ShouldBe(0);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain("No items to migrate");
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnCorrectItemCount()
	{
		// Arrange
		var policy = MigrationPolicy.Default;
		const int itemCount = 1000;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, itemCount, 0, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnCorrectDataSize()
	{
		// Arrange
		var policy = MigrationPolicy.Default;
		const long dataSizeBytes = 1024 * 1024 * 100; // 100MB

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 100, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedDataSizeBytes.ShouldBe(dataSizeBytes);
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_CalculateDuration_UsingFormula()
	{
		// Arrange
		// Formula: (itemCount × 3ms + dataSizeBytes / 100MB/s) × 1.2
		var policy = MigrationPolicy.Default;
		const int itemCount = 1000;
		const long dataSizeBytes = 100 * 1024 * 1024; // 100MB

		const double msPerItem = 3.0;
		const double ioOverheadFactor = 1.2;
		const long ioBytesPerSecond = 100 * 1024 * 1024; // 100 MB/s

		var encryptionMs = itemCount * msPerItem;
		var ioMs = dataSizeBytes / (double)ioBytesPerSecond * 1000;
		var expectedMs = (encryptionMs + ioMs) * ioOverheadFactor;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, itemCount, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedDuration.TotalMilliseconds.ShouldBe(expectedMs);
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnEstimatedAt_WithCurrentTime()
	{
		// Arrange
		var policy = MigrationPolicy.Default;
		var beforeCall = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 100, 1024, CancellationToken.None);

		// Assert
		var afterCall = DateTimeOffset.UtcNow;
		result.EstimatedAt.ShouldBeGreaterThanOrEqualTo(beforeCall);
		result.EstimatedAt.ShouldBeLessThanOrEqualTo(afterCall);
	}

	[Theory]
	[InlineData(1, 0)]
	[InlineData(100, 1024)]
	[InlineData(10_000, 1024 * 1024)]
	[InlineData(1_000_000, 1024 * 1024 * 100)]
	public async Task EstimateMigrationAsyncTyped_ScaleWithItemCountAndDataSize(int itemCount, long dataSizeBytes)
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, itemCount, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
		result.EstimatedDataSizeBytes.ShouldBe(dataSizeBytes);
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnEmptyWarnings_ForNormalEstimate()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 100, 1024, CancellationToken.None);

		// Assert
		result.Warnings.ShouldBeNull(); // No warnings for normal estimates
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnWarning_WhenMaxKeyAgeTooShort()
	{
		// Arrange
		var policy = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(20) };

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 100, 1024, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("frequent migrations", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnWarning_WhenDurationExceedsOneHour()
	{
		// Arrange - Calculate item count that results in >1 hour duration
		// Formula: (itemCount × 3ms + dataSizeBytes / 100MB/s) × 1.2 > 3600000ms
		// Simplify: itemCount × 3.6ms > 3600000ms => itemCount > 1,000,000
		var policy = MigrationPolicy.Default;
		const int largeItemCount = 2_000_000;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, largeItemCount, 0, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("1 hour", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_HandleLargeDataSize()
	{
		// Arrange - 10GB of data
		var policy = MigrationPolicy.Default;
		const long tenGigabytes = 10L * 1024 * 1024 * 1024;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 100, tenGigabytes, CancellationToken.None);

		// Assert
		result.EstimatedDataSizeBytes.ShouldBe(tenGigabytes);
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task EstimateMigrationAsyncTyped_ReturnNullWarnings_WhenNoIssues()
	{
		// Arrange - Small, fast migration
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _sut.EstimateMigrationAsync(policy, 10, 1024, CancellationToken.None);

		// Assert
		result.Warnings.ShouldBeNull();
	}

	#endregion EstimateMigrationAsync (typed) Tests

	#region Helper Methods

	private static EncryptedData CreateEncryptedData(
		string keyId = "test-key",
		EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes256Gcm)
	{
		return new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3, 4, 5 },
			KeyId = keyId,
			KeyVersion = 1,
			Algorithm = algorithm,
			Iv = new byte[12],
			AuthTag = new byte[16],
			EncryptedAt = DateTimeOffset.UtcNow
		};
	}

	private static EncryptionContext CreateEncryptionContext(string? keyId = null)
	{
		return new EncryptionContext
		{
			KeyId = keyId ?? "test-key",
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};
	}

	private static EncryptionMigrationItem CreateMigrationItem(string itemId, string keyId)
	{
		return new EncryptionMigrationItem
		{
			ItemId = itemId,
			EncryptedData = CreateEncryptedData(keyId),
			SourceContext = CreateEncryptionContext(keyId)
		};
	}

	#endregion Helper Methods
}
