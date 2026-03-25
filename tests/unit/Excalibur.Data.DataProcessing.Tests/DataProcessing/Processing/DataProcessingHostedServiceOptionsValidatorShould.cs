// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Processing;

namespace Excalibur.Data.Tests.DataProcessing.Processing;

/// <summary>
/// Unit tests for <see cref="DataProcessingHostedServiceOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProcessingHostedServiceOptionsValidatorShould : UnitTestBase
{
	private readonly DataProcessingHostedServiceOptionsValidator _sut = new();

	[Fact]
	public void Succeed_WithValidDefaults()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenPollingIntervalIsZero()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.Zero,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PollingInterval");
	}

	[Fact]
	public void Fail_WhenPollingIntervalIsNegative()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromSeconds(-1),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PollingInterval");
	}

	[Fact]
	public void Fail_WhenDrainTimeoutLessThanOrEqualToPollingInterval()
	{
		// Arrange -- DrainTimeout (5s) == PollingInterval (5s)
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 5,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DrainTimeoutSeconds");
	}

	[Fact]
	public void Fail_WhenDrainTimeoutLessThanPollingInterval()
	{
		// Arrange -- DrainTimeout (3s) < PollingInterval (5s)
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 3,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DrainTimeoutSeconds");
	}

	[Fact]
	public void Fail_WhenUnhealthyThresholdIsZero()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions
		{
			UnhealthyThreshold = 0,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("UnhealthyThreshold");
	}

	[Fact]
	public void Succeed_WhenDrainTimeoutGreaterThanPollingInterval()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 10,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		// Arrange -- Both PollingInterval and UnhealthyThreshold invalid
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.Zero,
			UnhealthyThreshold = 0,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PollingInterval");
		result.FailureMessage.ShouldContain("UnhealthyThreshold");
	}
}
