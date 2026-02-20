// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceOptionsExtendedShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var options = new PersistenceOptions();

		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableSensitiveDataLogging.ShouldBeFalse();
		options.DefaultCommandTimeout.ShouldBe(30);
		options.DefaultIsolationLevel.ShouldBe(System.Data.IsolationLevel.ReadCommitted);
		options.EnableAutoRetry.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelayMilliseconds.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var options = new PersistenceOptions
		{
			EnableTracing = false,
			EnableMetrics = false,
			EnableSensitiveDataLogging = true,
			DefaultCommandTimeout = 60,
			DefaultIsolationLevel = System.Data.IsolationLevel.Serializable,
			EnableAutoRetry = false,
			MaxRetryAttempts = 5,
			RetryDelayMilliseconds = 500
		};

		options.EnableTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableSensitiveDataLogging.ShouldBeTrue();
		options.DefaultCommandTimeout.ShouldBe(60);
		options.DefaultIsolationLevel.ShouldBe(System.Data.IsolationLevel.Serializable);
		options.EnableAutoRetry.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryDelayMilliseconds.ShouldBe(500);
	}
}
