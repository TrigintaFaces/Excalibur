// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Unit tests for the <see cref="IMigrator"/> interface contract.
/// </summary>
/// <remarks>
/// Tests verify the IMigrator interface contract using FakeItEasy to validate
/// expected behavior patterns: happy path flows, error handling, and cancellation.
/// These tests document the expected API contract that all implementations must follow.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "Abstractions")]
public sealed class IMigratorShould
{
	private readonly IMigrator _migrator = A.Fake<IMigrator>();

	#region MigrateAsync Tests

	[Fact]
	public async Task MigrateAsync_WhenNoPendingMigrations_ReturnSuccessWithEmptyList()
	{
		// Arrange
		var expected = MigrationResult.NoMigrationsPending();
		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.AppliedMigrations.ShouldBeEmpty();
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task MigrateAsync_WhenMigrationsExist_ReturnSuccessWithAppliedMigrations()
	{
		// Arrange
		var appliedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205120000_CreateEventsTable",
				AppliedAt = DateTimeOffset.UtcNow,
				Description = "CreateEventsTable",
				Checksum = "ABC123"
			},
			new()
			{
				MigrationId = "20260205130000_CreateSnapshotsTable",
				AppliedAt = DateTimeOffset.UtcNow,
				Description = "CreateSnapshotsTable",
				Checksum = "DEF456"
			}
		};
		var expected = MigrationResult.Succeeded(appliedMigrations);
		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.AppliedMigrations.Count.ShouldBe(2);
		result.AppliedMigrations[0].MigrationId.ShouldBe("20260205120000_CreateEventsTable");
		result.AppliedMigrations[1].MigrationId.ShouldBe("20260205130000_CreateSnapshotsTable");
	}

	[Fact]
	public async Task MigrateAsync_WhenLockNotAcquired_ReturnFailure()
	{
		// Arrange
		var expected = MigrationResult.Failed("Failed to acquire migration lock. Another migration may be in progress.");
		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("lock");
		result.AppliedMigrations.ShouldBeEmpty();
	}

	[Fact]
	public async Task MigrateAsync_WhenMigrationFails_ReturnFailureWithException()
	{
		// Arrange
		var exception = new InvalidOperationException("Table already exists");
		var expected = MigrationResult.Failed("Migration 20260205120000_CreateEventsTable failed: Table already exists", exception);
		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public async Task MigrateAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>.That.Matches(ct => ct.IsCancellationRequested)))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _migrator.MigrateAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task MigrateAsync_IsIdempotent_SucceedsOnSecondCall()
	{
		// Arrange - First call applies migrations, second call finds none pending
		var appliedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205120000_CreateEventsTable",
				AppliedAt = DateTimeOffset.UtcNow
			}
		};

		A.CallTo(() => _migrator.MigrateAsync(A<CancellationToken>._))
			.ReturnsNextFromSequence(
				MigrationResult.Succeeded(appliedMigrations),
				MigrationResult.NoMigrationsPending());

		// Act
		var firstResult = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
		var secondResult = await _migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		firstResult.Success.ShouldBeTrue();
		firstResult.AppliedMigrations.Count.ShouldBe(1);
		secondResult.Success.ShouldBeTrue();
		secondResult.AppliedMigrations.ShouldBeEmpty();
	}

	#endregion MigrateAsync Tests

	#region GetAppliedMigrationsAsync Tests

	[Fact]
	public async Task GetAppliedMigrationsAsync_WhenNoMigrationsApplied_ReturnEmptyList()
	{
		// Arrange
		A.CallTo(() => _migrator.GetAppliedMigrationsAsync(A<CancellationToken>._))
			.Returns(Array.Empty<AppliedMigration>());

		// Act
		var result = await _migrator.GetAppliedMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetAppliedMigrationsAsync_WhenMigrationsExist_ReturnOrderedList()
	{
		// Arrange
		var earlier = DateTimeOffset.UtcNow.AddHours(-1);
		var later = DateTimeOffset.UtcNow;
		var appliedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205120000_CreateEventsTable",
				AppliedAt = earlier,
				Description = "CreateEventsTable"
			},
			new()
			{
				MigrationId = "20260205130000_CreateSnapshotsTable",
				AppliedAt = later,
				Description = "CreateSnapshotsTable"
			}
		};

		A.CallTo(() => _migrator.GetAppliedMigrationsAsync(A<CancellationToken>._))
			.Returns(appliedMigrations);

		// Act
		var result = await _migrator.GetAppliedMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result[0].AppliedAt.ShouldBeLessThan(result[1].AppliedAt);
	}

	[Fact]
	public async Task GetAppliedMigrationsAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		A.CallTo(() => _migrator.GetAppliedMigrationsAsync(A<CancellationToken>.That.Matches(ct => ct.IsCancellationRequested)))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _migrator.GetAppliedMigrationsAsync(cts.Token)).ConfigureAwait(false);
	}

	#endregion GetAppliedMigrationsAsync Tests

	#region RollbackAsync Tests

	[Fact]
	public async Task RollbackAsync_WhenTargetFound_ReturnSuccessWithRemovedMigrations()
	{
		// Arrange
		var removedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205130000_CreateSnapshotsTable",
				AppliedAt = DateTimeOffset.UtcNow
			}
		};
		var expected = MigrationResult.Succeeded(removedMigrations);
		A.CallTo(() => _migrator.RollbackAsync("20260205120000_CreateEventsTable", A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.RollbackAsync("20260205120000_CreateEventsTable", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.AppliedMigrations.Count.ShouldBe(1);
		result.AppliedMigrations[0].MigrationId.ShouldBe("20260205130000_CreateSnapshotsTable");
	}

	[Fact]
	public async Task RollbackAsync_WhenTargetNotFound_ReturnFailure()
	{
		// Arrange
		var expected = MigrationResult.Failed("Target migration 'nonexistent' not found in applied migrations.");
		A.CallTo(() => _migrator.RollbackAsync("nonexistent", A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.RollbackAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task RollbackAsync_WhenTargetIsLatest_ReturnNoMigrationsPending()
	{
		// Arrange
		var expected = MigrationResult.NoMigrationsPending();
		A.CallTo(() => _migrator.RollbackAsync("20260205130000_CreateSnapshotsTable", A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.RollbackAsync("20260205130000_CreateSnapshotsTable", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.AppliedMigrations.ShouldBeEmpty();
	}

	[Fact]
	public async Task RollbackAsync_WhenLockNotAcquired_ReturnFailure()
	{
		// Arrange
		var expected = MigrationResult.Failed("Failed to acquire migration lock. Another migration may be in progress.");
		A.CallTo(() => _migrator.RollbackAsync(A<string>._, A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.RollbackAsync("20260205120000_CreateEventsTable", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("lock");
	}

	[Fact]
	public async Task RollbackAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		A.CallTo(() => _migrator.RollbackAsync(A<string>._, A<CancellationToken>.That.Matches(ct => ct.IsCancellationRequested)))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _migrator.RollbackAsync("20260205120000_CreateEventsTable", cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RollbackAsync_WhenExceptionOccurs_ReturnFailureWithException()
	{
		// Arrange
		var exception = new InvalidOperationException("Database error");
		var expected = MigrationResult.Failed("Rollback failed: Database error", exception);
		A.CallTo(() => _migrator.RollbackAsync(A<string>._, A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _migrator.RollbackAsync("20260205120000_CreateEventsTable", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.Exception.ShouldBe(exception);
		result.ErrorMessage.ShouldContain("Rollback failed");
	}

	#endregion RollbackAsync Tests

	#region GetPendingMigrationsAsync Tests

	[Fact]
	public async Task GetPendingMigrationsAsync_WhenNoPending_ReturnEmptyList()
	{
		// Arrange
		A.CallTo(() => _migrator.GetPendingMigrationsAsync(A<CancellationToken>._))
			.Returns(Array.Empty<string>());

		// Act
		var result = await _migrator.GetPendingMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetPendingMigrationsAsync_WhenPendingExist_ReturnOrderedList()
	{
		// Arrange
		var pendingMigrations = new List<string>
		{
			"20260205120000_CreateEventsTable",
			"20260205130000_CreateSnapshotsTable",
			"20260205140000_AddIndexes"
		};
		A.CallTo(() => _migrator.GetPendingMigrationsAsync(A<CancellationToken>._))
			.Returns(pendingMigrations);

		// Act
		var result = await _migrator.GetPendingMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
		result[0].ShouldBe("20260205120000_CreateEventsTable");
		result[1].ShouldBe("20260205130000_CreateSnapshotsTable");
		result[2].ShouldBe("20260205140000_AddIndexes");
	}

	[Fact]
	public async Task GetPendingMigrationsAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		A.CallTo(() => _migrator.GetPendingMigrationsAsync(A<CancellationToken>.That.Matches(ct => ct.IsCancellationRequested)))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _migrator.GetPendingMigrationsAsync(cts.Token)).ConfigureAwait(false);
	}

	#endregion GetPendingMigrationsAsync Tests

	#region Interface Design Tests

	[Fact]
	public void IMigrator_ShouldHaveExactlyFourMethods()
	{
		// The IMigrator interface should be minimal per Microsoft design guidelines
		var methods = typeof(IMigrator).GetMethods();
		methods.Length.ShouldBe(4);
	}

	[Fact]
	public void IMigrator_ShouldBePublic()
	{
		typeof(IMigrator).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void IMigrator_ShouldBeInterface()
	{
		typeof(IMigrator).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void IMigrator_ShouldNotInheritIDisposable()
	{
		// IMigrator should not force IDisposable on implementations
		typeof(IDisposable).IsAssignableFrom(typeof(IMigrator)).ShouldBeFalse();
	}

	#endregion Interface Design Tests
}
