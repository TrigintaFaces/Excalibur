// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="MigrationStatus"/> record and <see cref="MigrationState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class MigrationStatusShould : UnitTestBase
{
	[Fact]
	public void CreateValidMigrationStatusWithRequiredProperties()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;

		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-001",
			State = MigrationState.Running,
			TotalItems = 1000,
			CompletedItems = 250,
			SucceededItems = 250,
			FailedItems = 0,
			StartedAt = startedAt,
			LastUpdatedAt = startedAt
		};

		// Assert
		status.MigrationId.ShouldBe("mig-001");
		status.State.ShouldBe(MigrationState.Running);
		status.TotalItems.ShouldBe(1000);
		status.CompletedItems.ShouldBe(250);
		status.SucceededItems.ShouldBe(250);
		status.FailedItems.ShouldBe(0);
		status.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void CalculatePercentComplete()
	{
		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-pct",
			State = MigrationState.Running,
			TotalItems = 100,
			CompletedItems = 25,
			SucceededItems = 25,
			FailedItems = 0,
			StartedAt = DateTimeOffset.UtcNow,
			LastUpdatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		status.PercentComplete.ShouldBe(25.0);
	}

	[Fact]
	public void HandleZeroTotalItemsInPercentComplete()
	{
		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-zero",
			State = MigrationState.Pending,
			TotalItems = 0,
			CompletedItems = 0,
			SucceededItems = 0,
			FailedItems = 0,
			StartedAt = DateTimeOffset.UtcNow,
			LastUpdatedAt = DateTimeOffset.UtcNow
		};

		// Assert - Should return 0, not NaN or infinity
		status.PercentComplete.ShouldBe(0);
	}

	[Fact]
	public void CreateCompletedMigrationStatus()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-done",
			State = MigrationState.Completed,
			TotalItems = 500,
			CompletedItems = 500,
			SucceededItems = 498,
			FailedItems = 2,
			StartedAt = startedAt,
			LastUpdatedAt = completedAt,
			CompletedAt = completedAt
		};

		// Assert
		status.State.ShouldBe(MigrationState.Completed);
		status.CompletedAt.ShouldBe(completedAt);
		status.PercentComplete.ShouldBe(100.0);
	}

	[Fact]
	public void CreateFailedMigrationStatusWithErrorMessage()
	{
		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-fail",
			State = MigrationState.Failed,
			TotalItems = 100,
			CompletedItems = 50,
			SucceededItems = 45,
			FailedItems = 5,
			StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			LastUpdatedAt = DateTimeOffset.UtcNow,
			ErrorMessage = "Connection timeout after 5 retries"
		};

		// Assert
		status.State.ShouldBe(MigrationState.Failed);
		status.ErrorMessage.ShouldBe("Connection timeout after 5 retries");
	}

	[Fact]
	public void SupportDetailsMetadata()
	{
		// Arrange
		var details = new Dictionary<string, string>
		{
			["source"] = "legacy-provider",
			["target"] = "new-provider",
			["batch-size"] = "100"
		};

		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-details",
			State = MigrationState.Running,
			TotalItems = 1000,
			CompletedItems = 0,
			SucceededItems = 0,
			FailedItems = 0,
			StartedAt = DateTimeOffset.UtcNow,
			LastUpdatedAt = DateTimeOffset.UtcNow,
			Details = details
		};

		// Assert
		_ = status.Details.ShouldNotBeNull();
		status.Details.Count.ShouldBe(3);
		status.Details["source"].ShouldBe("legacy-provider");
	}

	[Theory]
	[InlineData(MigrationState.Pending)]
	[InlineData(MigrationState.Running)]
	[InlineData(MigrationState.Paused)]
	[InlineData(MigrationState.Completed)]
	[InlineData(MigrationState.Failed)]
	[InlineData(MigrationState.Cancelled)]
	public void SupportAllMigrationStates(MigrationState state)
	{
		// Act
		var status = new MigrationStatus
		{
			MigrationId = "mig-state",
			State = state,
			TotalItems = 100,
			CompletedItems = 0,
			SucceededItems = 0,
			FailedItems = 0,
			StartedAt = DateTimeOffset.UtcNow,
			LastUpdatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		status.State.ShouldBe(state);
	}

	[Fact]
	public void HavePendingAsDefaultState()
	{
		// Arrange
		MigrationState defaultValue = default;

		// Assert
		defaultValue.ShouldBe(MigrationState.Pending);
	}

	[Theory]
	[InlineData(MigrationState.Pending, 0)]
	[InlineData(MigrationState.Running, 1)]
	[InlineData(MigrationState.Paused, 2)]
	[InlineData(MigrationState.Completed, 3)]
	[InlineData(MigrationState.Failed, 4)]
	[InlineData(MigrationState.Cancelled, 5)]
	public void HaveCorrectStateUnderlyingValues(MigrationState state, int expectedValue)
	{
		// Assert
		((int)state).ShouldBe(expectedValue);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;

		var status1 = new MigrationStatus
		{
			MigrationId = "mig-eq",
			State = MigrationState.Running,
			TotalItems = 100,
			CompletedItems = 50,
			SucceededItems = 50,
			FailedItems = 0,
			StartedAt = startedAt,
			LastUpdatedAt = startedAt
		};

		var status2 = new MigrationStatus
		{
			MigrationId = "mig-eq",
			State = MigrationState.Running,
			TotalItems = 100,
			CompletedItems = 50,
			SucceededItems = 50,
			FailedItems = 0,
			StartedAt = startedAt,
			LastUpdatedAt = startedAt
		};

		// Assert
		status1.ShouldBe(status2);
	}
}
