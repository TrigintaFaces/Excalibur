// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AppendResult"/> to verify success, failure, and concurrency conflict behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AppendResultShould
{
	[Fact]
	public void CreateSuccessResult_WithValidParameters()
	{
		// Arrange
		const long nextExpectedVersion = 5;
		const long firstEventPosition = 100;

		// Act
		var result = AppendResult.CreateSuccess(nextExpectedVersion, firstEventPosition);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(nextExpectedVersion);
		result.FirstEventPosition.ShouldBe(firstEventPosition);
		result.ErrorMessage.ShouldBeNull();
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void CreateConcurrencyConflict_WithVersionMismatch()
	{
		// Arrange
		const long expectedVersion = 3;
		const long actualVersion = 5;

		// Act
		var result = AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);

		// Assert
		result.Success.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(actualVersion);
		result.FirstEventPosition.ShouldBe(-1);
		_ = result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("version");
		result.IsConcurrencyConflict.ShouldBeTrue();
	}

	[Fact]
	public void CreateFailure_WithCustomErrorMessage()
	{
		// Arrange
		const string errorMessage = "Custom error occurred";

		// Act
		var result = AppendResult.CreateFailure(errorMessage);

		// Assert
		result.Success.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(-1);
		result.FirstEventPosition.ShouldBe(-1);
		result.ErrorMessage.ShouldBe(errorMessage);
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void IsConcurrencyConflict_ReturnsFalse_WhenSuccessful()
	{
		// Arrange & Act
		var result = AppendResult.CreateSuccess(1, 1);

		// Assert
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void IsConcurrencyConflict_ReturnsFalse_WhenFailureWithoutVersionInMessage()
	{
		// Arrange & Act
		var result = AppendResult.CreateFailure("Some other error");

		// Assert
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void ConcurrencyConflict_ErrorMessage_ContainsExpectedAndActualVersions()
	{
		// Arrange
		const long expectedVersion = 10;
		const long actualVersion = 15;

		// Act
		var result = AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);

		// Assert
		result.ErrorMessage.ShouldContain("10");
		result.ErrorMessage.ShouldContain("15");
	}

	[Fact]
	public void CreateSuccess_WithZeroPosition_IsValid()
	{
		// Arrange & Act
		var result = AppendResult.CreateSuccess(0, 0);

		// Assert
		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(0);
		result.FirstEventPosition.ShouldBe(0);
	}
}
