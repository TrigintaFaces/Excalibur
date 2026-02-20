// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies cross-property validation in <see cref="ProjectionOptionsValidator"/>.
/// Sprint 564 S564.54: Projection IValidateOptions tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionOptionsValidatorShould
{
	private readonly ProjectionOptionsValidator _sut = new();

	[Fact]
	public void Succeed_WithValidDefaults()
	{
		var options = new ProjectionOptions();
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#region IndexPrefix

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Fail_WhenIndexPrefixIsNullOrWhitespace(string? value)
	{
		var options = new ProjectionOptions { IndexPrefix = value! };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProjectionOptions.IndexPrefix));
	}

	#endregion

	#region RetryPolicy

	[Fact]
	public void Fail_WhenRetryBaseDelayExceedsMaxDelay()
	{
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				BaseDelay = TimeSpan.FromSeconds(60),
				MaxDelay = TimeSpan.FromSeconds(10),
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProjectionRetryOptions.BaseDelay));
	}

	[Fact]
	public void Succeed_WhenRetryBaseDelayEqualsMaxDelay()
	{
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				BaseDelay = TimeSpan.FromSeconds(10),
				MaxDelay = TimeSpan.FromSeconds(10),
			},
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenRetryMaxIndexAttemptsIsZero()
	{
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				MaxIndexAttempts = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProjectionRetryOptions.MaxIndexAttempts));
	}

	[Fact]
	public void Fail_WhenRetryMaxBulkAttemptsIsZero()
	{
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				MaxBulkAttempts = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProjectionRetryOptions.MaxBulkAttempts));
	}

	[Fact]
	public void Skip_RetryValidation_WhenDisabled()
	{
		var options = new ProjectionOptions
		{
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = false,
				BaseDelay = TimeSpan.FromSeconds(60),
				MaxDelay = TimeSpan.FromSeconds(10),
				MaxIndexAttempts = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region RebuildManager

	[Fact]
	public void Fail_WhenRebuildBatchSizeIsZero()
	{
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				DefaultBatchSize = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RebuildManagerOptions.DefaultBatchSize));
	}

	[Fact]
	public void Fail_WhenRebuildMaxDegreeOfParallelismIsZero()
	{
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				MaxDegreeOfParallelism = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RebuildManagerOptions.MaxDegreeOfParallelism));
	}

	[Fact]
	public void Skip_RebuildValidation_WhenDisabled()
	{
		var options = new ProjectionOptions
		{
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = false,
				DefaultBatchSize = 0,
				MaxDegreeOfParallelism = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region ConsistencyTracking

	[Theory]
	[InlineData(0.0)]
	[InlineData(-1.0)]
	[InlineData(101.0)]
	public void Fail_WhenSLAPercentageOutOfRange(double value)
	{
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				SLAPercentage = value,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ConsistencyTrackingOptions.SLAPercentage));
	}

	[Fact]
	public void Fail_WhenMetricsIntervalIsZero()
	{
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				MetricsInterval = TimeSpan.Zero,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ConsistencyTrackingOptions.MetricsInterval));
	}

	[Fact]
	public void Fail_WhenExpectedMaxLagIsZero()
	{
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = true,
				ExpectedMaxLag = TimeSpan.Zero,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ConsistencyTrackingOptions.ExpectedMaxLag));
	}

	[Fact]
	public void Skip_ConsistencyTrackingValidation_WhenDisabled()
	{
		var options = new ProjectionOptions
		{
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = false,
				SLAPercentage = 0.0,
				MetricsInterval = TimeSpan.Zero,
				ExpectedMaxLag = TimeSpan.Zero,
			},
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	[Fact]
	public void CollectMultipleFailures()
	{
		var options = new ProjectionOptions
		{
			IndexPrefix = "",
			RetryPolicy = new ProjectionRetryOptions
			{
				Enabled = true,
				BaseDelay = TimeSpan.FromSeconds(60),
				MaxDelay = TimeSpan.FromSeconds(10),
				MaxIndexAttempts = 0,
			},
			RebuildManager = new RebuildManagerOptions
			{
				Enabled = true,
				DefaultBatchSize = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProjectionOptions.IndexPrefix));
		result.FailureMessage.ShouldContain(nameof(ProjectionRetryOptions.BaseDelay));
		result.FailureMessage.ShouldContain(nameof(RebuildManagerOptions.DefaultBatchSize));
	}
}
