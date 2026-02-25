// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaInstanceInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaInstanceInfoShould
{
	#region Constructor Tests

	[Fact]
	public void InitializeWithAllProperties()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow.AddMinutes(-10);
		var lastUpdatedAt = DateTime.UtcNow.AddMinutes(-5);
		var completedAt = DateTime.UtcNow;

		// Act
		var info = new SagaInstanceInfo(
			SagaId: sagaId,
			SagaType: "OrderSaga",
			IsCompleted: true,
			CreatedAt: createdAt,
			LastUpdatedAt: lastUpdatedAt,
			CompletedAt: completedAt,
			FailureReason: null);

		// Assert
		info.SagaId.ShouldBe(sagaId);
		info.SagaType.ShouldBe("OrderSaga");
		info.IsCompleted.ShouldBeTrue();
		info.CreatedAt.ShouldBe(createdAt);
		info.LastUpdatedAt.ShouldBe(lastUpdatedAt);
		info.CompletedAt.ShouldBe(completedAt);
		info.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void AllowNullCompletedAt_ForRunningSagas()
	{
		// Arrange & Act
		var info = new SagaInstanceInfo(
			SagaId: Guid.NewGuid(),
			SagaType: "OrderSaga",
			IsCompleted: false,
			CreatedAt: DateTime.UtcNow,
			LastUpdatedAt: DateTime.UtcNow,
			CompletedAt: null,
			FailureReason: null);

		// Assert
		info.IsCompleted.ShouldBeFalse();
		info.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void StoreFailureReason_ForFailedSagas()
	{
		// Arrange & Act
		var info = new SagaInstanceInfo(
			SagaId: Guid.NewGuid(),
			SagaType: "PaymentSaga",
			IsCompleted: true,
			CreatedAt: DateTime.UtcNow,
			LastUpdatedAt: DateTime.UtcNow,
			CompletedAt: DateTime.UtcNow,
			FailureReason: "Payment gateway timeout");

		// Assert
		info.FailureReason.ShouldBe("Payment gateway timeout");
	}

	#endregion Constructor Tests

	#region Record Equality Tests

	[Fact]
	public void BeEqual_WhenAllPropertiesAreEqual()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;
		var lastUpdatedAt = DateTime.UtcNow;

		var info1 = new SagaInstanceInfo(sagaId, "OrderSaga", false, createdAt, lastUpdatedAt, null, null);
		var info2 = new SagaInstanceInfo(sagaId, "OrderSaga", false, createdAt, lastUpdatedAt, null, null);

		// Assert
		info1.ShouldBe(info2);
	}

	[Fact]
	public void NotBeEqual_WhenSagaIdDiffers()
	{
		// Arrange
		var createdAt = DateTime.UtcNow;

		var info1 = new SagaInstanceInfo(Guid.NewGuid(), "OrderSaga", false, createdAt, createdAt, null, null);
		var info2 = new SagaInstanceInfo(Guid.NewGuid(), "OrderSaga", false, createdAt, createdAt, null, null);

		// Assert
		info1.ShouldNotBe(info2);
	}

	[Fact]
	public void NotBeEqual_WhenSagaTypeDiffers()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;

		var info1 = new SagaInstanceInfo(sagaId, "OrderSaga", false, createdAt, createdAt, null, null);
		var info2 = new SagaInstanceInfo(sagaId, "PaymentSaga", false, createdAt, createdAt, null, null);

		// Assert
		info1.ShouldNotBe(info2);
	}

	[Fact]
	public void NotBeEqual_WhenIsCompletedDiffers()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;

		var info1 = new SagaInstanceInfo(sagaId, "OrderSaga", true, createdAt, createdAt, createdAt, null);
		var info2 = new SagaInstanceInfo(sagaId, "OrderSaga", false, createdAt, createdAt, null, null);

		// Assert
		info1.ShouldNotBe(info2);
	}

	#endregion Record Equality Tests

	#region With Expression Tests

	[Fact]
	public void SupportWithExpression_ForIsCompleted()
	{
		// Arrange
		var original = new SagaInstanceInfo(
			Guid.NewGuid(),
			"OrderSaga",
			false,
			DateTime.UtcNow,
			DateTime.UtcNow,
			null,
			null);

		// Act
		var modified = original with { IsCompleted = true, CompletedAt = DateTime.UtcNow };

		// Assert
		modified.IsCompleted.ShouldBeTrue();
		modified.CompletedAt.ShouldNotBeNull();
		modified.SagaId.ShouldBe(original.SagaId);
	}

	[Fact]
	public void SupportWithExpression_ForFailureReason()
	{
		// Arrange
		var original = new SagaInstanceInfo(
			Guid.NewGuid(),
			"OrderSaga",
			false,
			DateTime.UtcNow,
			DateTime.UtcNow,
			null,
			null);

		// Act
		var modified = original with { FailureReason = "Network error" };

		// Assert
		modified.FailureReason.ShouldBe("Network error");
	}

	#endregion With Expression Tests

	#region Deconstruction Tests

	[Fact]
	public void SupportDeconstruction()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;
		var lastUpdatedAt = DateTime.UtcNow;

		var info = new SagaInstanceInfo(
			sagaId,
			"OrderSaga",
			true,
			createdAt,
			lastUpdatedAt,
			createdAt,
			"Test failure");

		// Act
		var (id, type, completed, created, updated, completedTime, failure) = info;

		// Assert
		id.ShouldBe(sagaId);
		type.ShouldBe("OrderSaga");
		completed.ShouldBeTrue();
		created.ShouldBe(createdAt);
		updated.ShouldBe(lastUpdatedAt);
		completedTime.ShouldBe(createdAt);
		failure.ShouldBe("Test failure");
	}

	#endregion Deconstruction Tests

	#region Saga Health Scenarios Tests

	[Fact]
	public void RepresentHealthySaga_WhenRunningAndRecentlyUpdated()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var info = new SagaInstanceInfo(
			Guid.NewGuid(),
			"OrderSaga",
			IsCompleted: false,
			CreatedAt: now.AddMinutes(-5),
			LastUpdatedAt: now,
			CompletedAt: null,
			FailureReason: null);

		// Assert - Healthy: not completed, no failure
		info.IsCompleted.ShouldBeFalse();
		info.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void RepresentCompletedSaga_WhenSuccessful()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var info = new SagaInstanceInfo(
			Guid.NewGuid(),
			"OrderSaga",
			IsCompleted: true,
			CreatedAt: now.AddMinutes(-10),
			LastUpdatedAt: now,
			CompletedAt: now,
			FailureReason: null);

		// Assert - Completed: is completed, no failure
		info.IsCompleted.ShouldBeTrue();
		info.CompletedAt.ShouldNotBeNull();
		info.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void RepresentFailedSaga_WhenHasFailureReason()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var info = new SagaInstanceInfo(
			Guid.NewGuid(),
			"OrderSaga",
			IsCompleted: true,
			CreatedAt: now.AddMinutes(-10),
			LastUpdatedAt: now,
			CompletedAt: now,
			FailureReason: "Payment declined");

		// Assert - Failed: has failure reason
		info.IsCompleted.ShouldBeTrue();
		info.FailureReason.ShouldNotBeNull();
	}

	#endregion Saga Health Scenarios Tests
}
