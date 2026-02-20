// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.Core.Views;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewOptionsValidatorShould2
{
	private readonly MaterializedViewOptionsValidator _sut = new();

	[Fact]
	public void SucceedWithValidOptions()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 100, BatchDelay = TimeSpan.FromMilliseconds(10) };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenBatchSizeIsTooSmall()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 0 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
	}

	[Fact]
	public void FailWhenBatchSizeIsTooLarge()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 10001 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
	}

	[Fact]
	public void FailWhenBatchDelayIsNegative()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchDelay");
	}

	[Fact]
	public void SucceedWithMinimumValidBatchSize()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 1 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithMaximumValidBatchSize()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 10000 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithZeroBatchDelay()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.Zero };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = 0, BatchDelay = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
		result.FailureMessage.ShouldContain("BatchDelay");
	}
}
