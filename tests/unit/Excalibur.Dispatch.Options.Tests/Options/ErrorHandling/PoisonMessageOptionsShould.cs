// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Options.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="PoisonMessageOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class PoisonMessageOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxRetryAttempts_IsThree()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_MaxProcessingTime_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_DeadLetterRetentionPeriod_IsThirtyDays()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.DeadLetterRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void Default_EnableAutoCleanup_IsTrue()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.EnableAutoCleanup.ShouldBeTrue();
	}

	[Fact]
	public void Default_AutoCleanupInterval_IsOneDay()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.AutoCleanupInterval.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void Default_CaptureExceptionDetails_IsTrue()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.CaptureExceptionDetails.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableAlerting_IsTrue()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.EnableAlerting.ShouldBeTrue();
	}

	[Fact]
	public void Default_AlertThreshold_IsTen()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.AlertThreshold.ShouldBe(10);
	}

	[Fact]
	public void Default_AlertTimeWindow_IsFifteenMinutes()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.AlertTimeWindow.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region PoisonExceptionTypes Tests

	[Fact]
	public void Default_PoisonExceptionTypes_ContainsExpectedTypes()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.PoisonExceptionTypes.ShouldContain(typeof(InvalidOperationException));
		options.PoisonExceptionTypes.ShouldContain(typeof(NotSupportedException));
		options.PoisonExceptionTypes.ShouldContain(typeof(FormatException));
		options.PoisonExceptionTypes.ShouldContain(typeof(ArgumentException));
		options.PoisonExceptionTypes.ShouldContain(typeof(TypeLoadException));
	}

	[Fact]
	public void Default_PoisonExceptionTypes_HasExpectedCount()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.PoisonExceptionTypes.Count.ShouldBe(5);
	}

	[Fact]
	public void PoisonExceptionTypes_CanAddNewType()
	{
		// Arrange
		var options = new PoisonMessageOptions();

		// Act
		_ = options.PoisonExceptionTypes.Add(typeof(ApplicationException));

		// Assert
		options.PoisonExceptionTypes.ShouldContain(typeof(ApplicationException));
	}

	#endregion

	#region TransientExceptionTypes Tests

	[Fact]
	public void Default_TransientExceptionTypes_ContainsExpectedTypes()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.TransientExceptionTypes.ShouldContain(typeof(TimeoutException));
		options.TransientExceptionTypes.ShouldContain(typeof(OperationCanceledException));
		options.TransientExceptionTypes.ShouldContain(typeof(TaskCanceledException));
	}

	[Fact]
	public void Default_TransientExceptionTypes_HasExpectedCount()
	{
		// Arrange & Act
		var options = new PoisonMessageOptions();

		// Assert
		options.TransientExceptionTypes.Count.ShouldBe(3);
	}

	[Fact]
	public void TransientExceptionTypes_CanAddNewType()
	{
		// Arrange
		var options = new PoisonMessageOptions();

		// Act
		_ = options.TransientExceptionTypes.Add(typeof(IOException));

		// Assert
		options.TransientExceptionTypes.ShouldContain(typeof(IOException));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
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

	#endregion
}
