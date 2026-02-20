// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="DeadLetterOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeadLetterOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxAttempts_IsThree()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_QueueName_IsDeadletter()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.QueueName.ShouldBe("deadletter");
	}

	[Fact]
	public void Default_PreserveMetadata_IsTrue()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.PreserveMetadata.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeExceptionDetails_IsTrue()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.IncludeExceptionDetails.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableRecovery_IsFalse()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.EnableRecovery.ShouldBeFalse();
	}

	[Fact]
	public void Default_RecoveryInterval_IsOneHour()
	{
		// Arrange & Act
		var options = new DeadLetterOptions();

		// Assert
		options.RecoveryInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxAttempts_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.MaxAttempts = 5;

		// Assert
		options.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void QueueName_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.QueueName = "custom-deadletter";

		// Assert
		options.QueueName.ShouldBe("custom-deadletter");
	}

	[Fact]
	public void PreserveMetadata_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.PreserveMetadata = false;

		// Assert
		options.PreserveMetadata.ShouldBeFalse();
	}

	[Fact]
	public void IncludeExceptionDetails_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.IncludeExceptionDetails = false;

		// Assert
		options.IncludeExceptionDetails.ShouldBeFalse();
	}

	[Fact]
	public void EnableRecovery_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.EnableRecovery = true;

		// Assert
		options.EnableRecovery.ShouldBeTrue();
	}

	[Fact]
	public void RecoveryInterval_CanBeSet()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.RecoveryInterval = TimeSpan.FromMinutes(30);

		// Assert
		options.RecoveryInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DeadLetterOptions
		{
			MaxAttempts = 10,
			QueueName = "errors",
			PreserveMetadata = false,
			IncludeExceptionDetails = false,
			EnableRecovery = true,
			RecoveryInterval = TimeSpan.FromMinutes(15),
		};

		// Assert
		options.MaxAttempts.ShouldBe(10);
		options.QueueName.ShouldBe("errors");
		options.PreserveMetadata.ShouldBeFalse();
		options.IncludeExceptionDetails.ShouldBeFalse();
		options.EnableRecovery.ShouldBeTrue();
		options.RecoveryInterval.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void MaxAttempts_CanBeZero()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.MaxAttempts = 0;

		// Assert
		options.MaxAttempts.ShouldBe(0);
	}

	[Fact]
	public void QueueName_CanBeEmpty()
	{
		// Arrange
		var options = new DeadLetterOptions();

		// Act
		options.QueueName = string.Empty;

		// Assert
		options.QueueName.ShouldBe(string.Empty);
	}

	#endregion
}
