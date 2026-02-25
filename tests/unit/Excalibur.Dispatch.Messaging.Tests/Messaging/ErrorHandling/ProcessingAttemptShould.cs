// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="ProcessingAttempt"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Priority", "0")]
public sealed class ProcessingAttemptShould
{
	#region Default Value Tests

	[Fact]
	public void Default_AttemptNumber_IsZero()
	{
		// Arrange & Act
		var attempt = new ProcessingAttempt();

		// Assert
		attempt.AttemptNumber.ShouldBe(0);
	}

	[Fact]
	public void Default_Succeeded_IsFalse()
	{
		// Arrange & Act
		var attempt = new ProcessingAttempt();

		// Assert
		attempt.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void Default_Duration_IsZero()
	{
		// Arrange & Act
		var attempt = new ProcessingAttempt();

		// Assert
		attempt.Duration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Default_OptionalProperties_AreNull()
	{
		// Arrange & Act
		var attempt = new ProcessingAttempt();

		// Assert
		attempt.ErrorMessage.ShouldBeNull();
		attempt.ExceptionType.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AttemptNumber_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();

		// Act
		attempt.AttemptNumber = 3;

		// Assert
		attempt.AttemptNumber.ShouldBe(3);
	}

	[Fact]
	public void AttemptTime_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();
		var expectedTime = DateTimeOffset.UtcNow;

		// Act
		attempt.AttemptTime = expectedTime;

		// Assert
		attempt.AttemptTime.ShouldBe(expectedTime);
	}

	[Fact]
	public void Duration_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();

		// Act
		attempt.Duration = TimeSpan.FromMilliseconds(150);

		// Assert
		attempt.Duration.TotalMilliseconds.ShouldBe(150);
	}

	[Fact]
	public void Succeeded_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();

		// Act
		attempt.Succeeded = true;

		// Assert
		attempt.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ErrorMessage_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();

		// Act
		attempt.ErrorMessage = "Database connection failed";

		// Assert
		attempt.ErrorMessage.ShouldBe("Database connection failed");
	}

	[Fact]
	public void ExceptionType_CanBeSet()
	{
		// Arrange
		var attempt = new ProcessingAttempt();

		// Act
		attempt.ExceptionType = "System.InvalidOperationException";

		// Assert
		attempt.ExceptionType.ShouldBe("System.InvalidOperationException");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 2,
			AttemptTime = DateTimeOffset.UtcNow,
			Duration = TimeSpan.FromSeconds(1.5),
			Succeeded = false,
			ErrorMessage = "Timeout",
			ExceptionType = "System.TimeoutException",
		};

		// Assert
		attempt.AttemptNumber.ShouldBe(2);
		attempt.Duration.TotalSeconds.ShouldBe(1.5);
		attempt.Succeeded.ShouldBeFalse();
		attempt.ErrorMessage.ShouldBe("Timeout");
		attempt.ExceptionType.ShouldBe("System.TimeoutException");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Attempt_ForSuccessfulProcessing_HasNoError()
	{
		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 1,
			AttemptTime = DateTimeOffset.UtcNow,
			Duration = TimeSpan.FromMilliseconds(50),
			Succeeded = true,
		};

		// Assert
		attempt.Succeeded.ShouldBeTrue();
		attempt.ErrorMessage.ShouldBeNull();
		attempt.ExceptionType.ShouldBeNull();
	}

	[Fact]
	public void Attempt_ForFailedProcessing_HasErrorDetails()
	{
		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 3,
			AttemptTime = DateTimeOffset.UtcNow,
			Duration = TimeSpan.FromSeconds(5),
			Succeeded = false,
			ErrorMessage = "Connection to database lost",
			ExceptionType = "System.Data.SqlClient.SqlException",
		};

		// Assert
		attempt.Succeeded.ShouldBeFalse();
		_ = attempt.ErrorMessage.ShouldNotBeNull();
		attempt.ExceptionType.ShouldContain("SqlException");
	}

	[Fact]
	public void Attempt_ForRetry_IncreasesAttemptNumber()
	{
		// Arrange
		var attempts = new List<ProcessingAttempt>();
		var baseTime = DateTimeOffset.UtcNow;

		// Act
		for (var i = 1; i <= 3; i++)
		{
			attempts.Add(new ProcessingAttempt
			{
				AttemptNumber = i,
				AttemptTime = baseTime.AddSeconds(i * 10),
				Duration = TimeSpan.FromMilliseconds(100 * i),
				Succeeded = i == 3,
				ErrorMessage = i < 3 ? "Transient failure" : null,
			});
		}

		// Assert
		attempts.Count.ShouldBe(3);
		attempts[0].AttemptNumber.ShouldBe(1);
		attempts[1].AttemptNumber.ShouldBe(2);
		attempts[2].AttemptNumber.ShouldBe(3);
		attempts.Last().Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Attempt_ForTimeout_ReflectsLongDuration()
	{
		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 1,
			AttemptTime = DateTimeOffset.UtcNow,
			Duration = TimeSpan.FromSeconds(30),
			Succeeded = false,
			ErrorMessage = "Operation timed out after 30 seconds",
			ExceptionType = "System.TimeoutException",
		};

		// Assert
		attempt.Duration.TotalSeconds.ShouldBe(30);
		attempt.ExceptionType.ShouldContain("Timeout");
	}

	#endregion
}
