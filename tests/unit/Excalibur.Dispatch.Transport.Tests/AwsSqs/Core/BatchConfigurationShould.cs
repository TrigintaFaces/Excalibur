// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class BatchConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new BatchConfiguration();

		// Assert
		config.MaxBatchSize.ShouldBe(10);
		config.BatchTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		config.EnableParallelProcessing.ShouldBeTrue();
		config.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
		config.EnableAutoRetry.ShouldBeTrue();
		config.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var config = new BatchConfiguration
		{
			MaxBatchSize = 50,
			BatchTimeout = TimeSpan.FromSeconds(30),
			EnableParallelProcessing = false,
			MaxDegreeOfParallelism = 4,
			EnableAutoRetry = false,
			MaxRetryAttempts = 10,
		};

		// Assert
		config.MaxBatchSize.ShouldBe(50);
		config.BatchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		config.EnableParallelProcessing.ShouldBeFalse();
		config.MaxDegreeOfParallelism.ShouldBe(4);
		config.EnableAutoRetry.ShouldBeFalse();
		config.MaxRetryAttempts.ShouldBe(10);
	}
}
