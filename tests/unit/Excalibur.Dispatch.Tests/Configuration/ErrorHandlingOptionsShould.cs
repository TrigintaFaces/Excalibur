// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ErrorHandlingOptionsShould
{
	// --- PoisonMessageOptions ---

	[Fact]
	public void PoisonMessageOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
		options.Cleanup.DeadLetterRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
		options.Cleanup.EnableAutoCleanup.ShouldBeTrue();
		options.Cleanup.AutoCleanupInterval.ShouldBe(TimeSpan.FromDays(1));
		options.CaptureExceptionDetails.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.Alerting.EnableAlerting.ShouldBeTrue();
		options.Alerting.AlertThreshold.ShouldBe(10);
		options.Alerting.AlertTimeWindow.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void PoisonMessageOptions_PoisonExceptionTypes_ContainExpectedDefaults()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.PoisonExceptionTypes.ShouldNotBeNull();
		options.PoisonExceptionTypes.Count.ShouldBe(5);
		options.PoisonExceptionTypes.ShouldContain(typeof(InvalidOperationException));
		options.PoisonExceptionTypes.ShouldContain(typeof(NotSupportedException));
		options.PoisonExceptionTypes.ShouldContain(typeof(FormatException));
		options.PoisonExceptionTypes.ShouldContain(typeof(ArgumentException));
		options.PoisonExceptionTypes.ShouldContain(typeof(TypeLoadException));
	}

	[Fact]
	public void PoisonMessageOptions_TransientExceptionTypes_ContainExpectedDefaults()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.TransientExceptionTypes.ShouldNotBeNull();
		options.TransientExceptionTypes.Count.ShouldBe(3);
		options.TransientExceptionTypes.ShouldContain(typeof(TimeoutException));
		options.TransientExceptionTypes.ShouldContain(typeof(OperationCanceledException));
		options.TransientExceptionTypes.ShouldContain(typeof(TaskCanceledException));
	}

	[Fact]
	public void PoisonMessageOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new PoisonMessageOptions
		{
			Enabled = false,
			MaxRetryAttempts = 5,
			MaxProcessingTime = TimeSpan.FromMinutes(10),
			Cleanup =
			{
				DeadLetterRetentionPeriod = TimeSpan.FromDays(60),
				EnableAutoCleanup = false,
				AutoCleanupInterval = TimeSpan.FromHours(12),
			},
			CaptureExceptionDetails = false,
			EnableMetrics = false,
			Alerting =
			{
				EnableAlerting = false,
				AlertThreshold = 20,
				AlertTimeWindow = TimeSpan.FromMinutes(30),
			},
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(10));
		options.Cleanup.DeadLetterRetentionPeriod.ShouldBe(TimeSpan.FromDays(60));
		options.Cleanup.EnableAutoCleanup.ShouldBeFalse();
		options.Cleanup.AutoCleanupInterval.ShouldBe(TimeSpan.FromHours(12));
		options.CaptureExceptionDetails.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.Alerting.EnableAlerting.ShouldBeFalse();
		options.Alerting.AlertThreshold.ShouldBe(20);
		options.Alerting.AlertTimeWindow.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
