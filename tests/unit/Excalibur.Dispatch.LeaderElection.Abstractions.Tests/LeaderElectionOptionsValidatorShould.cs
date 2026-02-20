// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaderElectionOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionOptionsValidatorShould : UnitTestBase
{
	private readonly LeaderElectionOptionsValidator _validator = new();

	[Fact]
	public void Validate_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void Validate_WithValidDefaults_ReturnsSuccess()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_RenewIntervalGreaterThanOrEqualLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewInterval");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void Validate_RenewIntervalGreaterThanLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(10),
			RenewInterval = TimeSpan.FromSeconds(20),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_GracePeriodGreaterThanOrEqualLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			GracePeriod = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriod");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void Validate_GracePeriodGreaterThanLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(10),
			GracePeriod = TimeSpan.FromSeconds(20),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_RenewIntervalLessThanLeaseDuration_Succeeds()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			GracePeriod = TimeSpan.FromSeconds(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_WithNameParameter_StillValidates()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Act
		var result = _validator.Validate("custom-name", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
