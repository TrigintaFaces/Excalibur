// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoisonMessageOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
		options.DeadLetterRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
		options.EnableAutoCleanup.ShouldBeTrue();
		options.AutoCleanupInterval.ShouldBe(TimeSpan.FromDays(1));
		options.CaptureExceptionDetails.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableAlerting.ShouldBeTrue();
		options.AlertThreshold.ShouldBe(10);
		options.AlertTimeWindow.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void PoisonExceptionTypes_ContainDefaultTypes()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.PoisonExceptionTypes.ShouldNotBeEmpty();
		options.PoisonExceptionTypes.ShouldContain(typeof(InvalidOperationException));
		options.PoisonExceptionTypes.ShouldContain(typeof(NotSupportedException));
		options.PoisonExceptionTypes.ShouldContain(typeof(FormatException));
		options.PoisonExceptionTypes.ShouldContain(typeof(ArgumentException));
		options.PoisonExceptionTypes.ShouldContain(typeof(TypeLoadException));
		options.PoisonExceptionTypes.Count.ShouldBe(5);
	}

	[Fact]
	public void TransientExceptionTypes_ContainDefaultTypes()
	{
		// Act
		var options = new PoisonMessageOptions();

		// Assert
		options.TransientExceptionTypes.ShouldNotBeEmpty();
		options.TransientExceptionTypes.ShouldContain(typeof(TimeoutException));
		options.TransientExceptionTypes.ShouldContain(typeof(OperationCanceledException));
		options.TransientExceptionTypes.ShouldContain(typeof(TaskCanceledException));
		options.TransientExceptionTypes.Count.ShouldBe(3);
	}

	[Fact]
	public void PoisonExceptionTypes_CanAddCustomTypes()
	{
		// Arrange
		var options = new PoisonMessageOptions();

		// Act
		options.PoisonExceptionTypes.Add(typeof(NullReferenceException));

		// Assert
		options.PoisonExceptionTypes.ShouldContain(typeof(NullReferenceException));
		options.PoisonExceptionTypes.Count.ShouldBe(6);
	}

	[Fact]
	public void AllProperties_AreSettable()
	{
		// Act
		var options = new PoisonMessageOptions
		{
			Enabled = false,
			MaxRetryAttempts = 5,
			MaxProcessingTime = TimeSpan.FromMinutes(10),
			DeadLetterRetentionPeriod = TimeSpan.FromDays(60),
			EnableAutoCleanup = false,
			AutoCleanupInterval = TimeSpan.FromHours(12),
			CaptureExceptionDetails = false,
			EnableMetrics = false,
			EnableAlerting = false,
			AlertThreshold = 20,
			AlertTimeWindow = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(10));
		options.DeadLetterRetentionPeriod.ShouldBe(TimeSpan.FromDays(60));
		options.EnableAutoCleanup.ShouldBeFalse();
		options.AutoCleanupInterval.ShouldBe(TimeSpan.FromHours(12));
		options.CaptureExceptionDetails.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableAlerting.ShouldBeFalse();
		options.AlertThreshold.ShouldBe(20);
		options.AlertTimeWindow.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
