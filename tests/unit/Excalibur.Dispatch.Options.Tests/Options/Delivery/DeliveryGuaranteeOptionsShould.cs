// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="DeliveryGuaranteeOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeliveryGuaranteeOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Guarantee_IsAtLeastOnce()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions();

		// Assert
		options.Guarantee.ShouldBe(DeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void Default_EnableIdempotencyTracking_IsTrue()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions();

		// Assert
		options.EnableIdempotencyTracking.ShouldBeTrue();
	}

	[Fact]
	public void Default_IdempotencyKeyRetention_IsSevenDays()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions();

		// Assert
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void Default_EnableAutomaticRetry_IsTrue()
	{
		// Arrange & Act
		var options = new DeliveryGuaranteeOptions();

		// Assert
		options.EnableAutomaticRetry.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Guarantee_CanBeSet()
	{
		// Arrange
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.Guarantee = DeliveryGuarantee.AtMostOnce;

		// Assert
		options.Guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
	}

	[Fact]
	public void EnableIdempotencyTracking_CanBeSet()
	{
		// Arrange
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.EnableIdempotencyTracking = false;

		// Assert
		options.EnableIdempotencyTracking.ShouldBeFalse();
	}

	[Fact]
	public void IdempotencyKeyRetention_CanBeSet()
	{
		// Arrange
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.IdempotencyKeyRetention = TimeSpan.FromDays(14);

		// Assert
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void EnableAutomaticRetry_CanBeSet()
	{
		// Arrange
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.EnableAutomaticRetry = false;

		// Assert
		options.EnableAutomaticRetry.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtMostOnce,
			EnableIdempotencyTracking = false,
			IdempotencyKeyRetention = TimeSpan.FromDays(3),
			EnableAutomaticRetry = false,
		};

		// Assert
		options.Guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
		options.EnableIdempotencyTracking.ShouldBeFalse();
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(3));
		options.EnableAutomaticRetry.ShouldBeFalse();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void IdempotencyKeyRetention_CanBeZero()
	{
		// Arrange
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.IdempotencyKeyRetention = TimeSpan.Zero;

		// Assert
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void IdempotencyKeyRetention_CanBeNegative()
	{
		// Note: Negative values might not make semantic sense,
		// but the type allows them (validation is separate concern)
		var options = new DeliveryGuaranteeOptions();

		// Act
		options.IdempotencyKeyRetention = TimeSpan.FromDays(-1);

		// Assert
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(-1));
	}

	#endregion
}
