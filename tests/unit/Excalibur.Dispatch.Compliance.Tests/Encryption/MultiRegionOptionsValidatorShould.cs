// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionOptionsValidatorShould
{
	private readonly MultiRegionOptionsValidator _validator = new();

	private static MultiRegionOptions CreateValidOptions() => new()
	{
		Primary = new RegionConfiguration
		{
			RegionId = "us-east-1",
			Endpoint = new Uri("https://primary.example.com"),
		},
		Secondary = new RegionConfiguration
		{
			RegionId = "eu-west-1",
			Endpoint = new Uri("https://secondary.example.com"),
		},
	};

	[Fact]
	public void SucceedForValidOptions()
	{
		var options = CreateValidOptions();

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenRpoTargetIsNotPositive(int seconds)
	{
		var options = CreateValidOptions();
		options.RpoTarget = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.RpoTarget));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenRtoTargetIsNotPositive(int seconds)
	{
		var options = CreateValidOptions();
		options.RtoTarget = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.RtoTarget));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenOperationTimeoutIsNotPositive(int seconds)
	{
		var options = CreateValidOptions();
		options.OperationTimeout = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.OperationTimeout));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenHealthCheckIntervalIsNotPositive(int seconds)
	{
		var options = CreateValidOptions();
		options.Failover.HealthCheckInterval = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionFailoverOptions.HealthCheckInterval));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenFailoverThresholdIsLessThanOne(int value)
	{
		var options = CreateValidOptions();
		options.Failover.FailoverThreshold = value;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionFailoverOptions.FailoverThreshold));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenAsyncReplicationIntervalIsNotPositive(int seconds)
	{
		var options = CreateValidOptions();
		options.Failover.AsyncReplicationInterval = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionFailoverOptions.AsyncReplicationInterval));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenPrimaryRegionIdIsEmpty(string regionId)
	{
		var options = CreateValidOptions();
		options.Primary.RegionId = regionId;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RegionConfiguration.RegionId));
	}

	[Fact]
	public void FailWhenPrimaryEndpointIsNull()
	{
		var options = CreateValidOptions();
		options.Primary.Endpoint = null!;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RegionConfiguration.Endpoint));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenSecondaryRegionIdIsEmpty(string regionId)
	{
		var options = CreateValidOptions();
		options.Secondary.RegionId = regionId;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RegionConfiguration.RegionId));
	}

	[Fact]
	public void FailWhenSecondaryEndpointIsNull()
	{
		var options = CreateValidOptions();
		options.Secondary.Endpoint = null!;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RegionConfiguration.Endpoint));
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		var options = CreateValidOptions();
		options.RpoTarget = TimeSpan.Zero;
		options.RtoTarget = TimeSpan.Zero;
		options.OperationTimeout = TimeSpan.Zero;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.RpoTarget));
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.RtoTarget));
		result.FailureMessage.ShouldContain(nameof(MultiRegionOptions.OperationTimeout));
	}
}
