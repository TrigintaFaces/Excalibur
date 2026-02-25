// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DlqProcessorOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new DlqProcessorOptions();

		// Assert
		options.BatchSize.ShouldBe(10);
		options.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new DlqProcessorOptions
		{
			BatchSize = 25,
			ProcessingTimeout = TimeSpan.FromMinutes(10),
			MaxRetryAttempts = 5,
		};

		// Assert
		options.BatchSize.ShouldBe(25);
		options.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxRetryAttempts.ShouldBe(5);
	}
}
