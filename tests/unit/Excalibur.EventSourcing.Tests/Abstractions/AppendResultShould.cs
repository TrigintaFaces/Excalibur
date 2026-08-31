// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AppendResult"/> to verify success, failure, and concurrency conflict behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
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
		result.ErrorMessage!.ShouldBeNull();
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
		result.FirstEventPosition.ShouldBeNull();
		_ = result.ErrorMessage!.ShouldNotBeNull();
		result.ErrorMessage!.ShouldContain("version");
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

		// NOT -1. Under this interface's version base -1 is the ordinary value meaning "this stream does
		// not exist", so a failure reporting it would hand a caller a number asserting the opposite of the
		// truth -- one they could pass straight back as an expected version and create a stream that
		// already holds events. A failure that has no version to report states none.
		result.NextExpectedVersion.ShouldBeNull();
		result.FirstEventPosition.ShouldBeNull();
		result.ErrorMessage!.ShouldBe(errorMessage);
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
	public void CreateConcurrencyConflict_OnANonExistentStream_ReportsATrueMinusOne()
	{
		// The one failure that CAN state a version: the store read the stream's actual version in order to
		// detect the conflict. Here that reading is -1, and it is TRUE -- the stream really does not exist.
		// This is what makes the null above a real distinction rather than a blanket ban on the value.
		var result = AppendResult.CreateConcurrencyConflict(expectedVersion: 4, actualVersion: -1);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(-1);
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
		result.ErrorMessage!.ShouldContain("10");
		result.ErrorMessage!.ShouldContain("15");
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
