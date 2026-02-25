// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionOptionsValidatorShould
{
	private readonly ProjectionOptionsValidator _sut = new();

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		// Arrange
		var options = new ProjectionOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenIndexPrefixIsEmpty()
	{
		// Arrange
		var options = new ProjectionOptions { IndexPrefix = "" };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("IndexPrefix");
	}

	[Fact]
	public void FailWhenIndexPrefixIsWhitespace()
	{
		// Arrange
		var options = new ProjectionOptions { IndexPrefix = "   " };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("IndexPrefix");
	}

	[Fact]
	public void FailWhenBaseDelayExceedsMaxDelay()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				BaseDelay = TimeSpan.FromMinutes(1),
				MaxDelay = TimeSpan.FromSeconds(5),
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BaseDelay");
		result.FailureMessage.ShouldContain("MaxDelay");
	}

	[Fact]
	public void SucceedWhenBaseDelayEqualsMaxDelay()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				BaseDelay = TimeSpan.FromSeconds(5),
				MaxDelay = TimeSpan.FromSeconds(5),
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenMaxIndexAttemptsIsZeroAndEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				MaxIndexAttempts = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxIndexAttempts");
	}

	[Fact]
	public void FailWhenMaxBulkAttemptsIsZeroAndEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				MaxBulkAttempts = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxBulkAttempts");
	}

	[Fact]
	public void SkipRetryValidationWhenDisabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = false,
				MaxIndexAttempts = 0,
				MaxBulkAttempts = 0,
				BaseDelay = TimeSpan.FromMinutes(10),
				MaxDelay = TimeSpan.FromSeconds(1),
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenDefaultBatchSizeIsZeroAndRebuildEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				DefaultBatchSize = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DefaultBatchSize");
	}

	[Fact]
	public void FailWhenMaxDegreeOfParallelismIsZeroAndRebuildEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				MaxDegreeOfParallelism = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxDegreeOfParallelism");
	}

	[Fact]
	public void SkipRebuildValidationWhenDisabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = false,
				DefaultBatchSize = 0,
				MaxDegreeOfParallelism = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenSLAPercentageIsZeroAndTrackingEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				SLAPercentage = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SLAPercentage");
	}

	[Fact]
	public void FailWhenSLAPercentageExceeds100()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				SLAPercentage = 101,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SLAPercentage");
	}

	[Fact]
	public void FailWhenMetricsIntervalIsZeroAndTrackingEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				MetricsInterval = TimeSpan.Zero,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MetricsInterval");
	}

	[Fact]
	public void FailWhenExpectedMaxLagIsZeroAndTrackingEnabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				ExpectedMaxLag = TimeSpan.Zero,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ExpectedMaxLag");
	}

	[Fact]
	public void SkipConsistencyTrackingValidationWhenDisabled()
	{
		// Arrange
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = false,
				SLAPercentage = 0,
				MetricsInterval = TimeSpan.Zero,
				ExpectedMaxLag = TimeSpan.Zero,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void CollectMultipleFailures()
	{
		// Arrange — multiple failures at once
		var options = new ProjectionOptions
		{
			IndexPrefix = "",
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				MaxIndexAttempts = 0,
				MaxBulkAttempts = 0,
			},
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				DefaultBatchSize = 0,
			},
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		// Should contain multiple failure messages
		result.FailureMessage.ShouldContain("IndexPrefix");
		result.FailureMessage.ShouldContain("MaxIndexAttempts");
		result.FailureMessage.ShouldContain("MaxBulkAttempts");
		result.FailureMessage.ShouldContain("DefaultBatchSize");
	}
}
