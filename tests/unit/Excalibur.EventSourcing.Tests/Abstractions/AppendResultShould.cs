// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	public void CreateConcurrencyConflict_WithNoMeasurement_ReportsNoVersionAndNamesNoneInTheMessage()
	{
		// The twin of the test above, and the reason the parameter is nullable at all. A store can detect a
		// conflict without succeeding in reading the version -- and the honest report is then NOTHING, not
		// the caller's own expected version. Were the caller's value echoed back here the result would read
		// "expected version 4 but current version is 4": a conflict asserting nothing moved.
		//
		// The -1 case above is what makes this a real distinction rather than a blanket ban: -1 means "the
		// stream measurably does not exist", null means "nobody looked". Collapsing them would leave a
		// caller unable to tell a measured empty stream from an unmeasured one.
		var result = AppendResult.CreateConcurrencyConflict(expectedVersion: 4, actualVersion: null);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBeNull(
			"a conflict the store could not measure states no version, so the caller reloads");

		// The message is the field the repository actually surfaces to a consumer, so an unmeasured conflict
		// must not name a current version there either.
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldNotContain(
			"current version is",
			Case.Sensitive,
			"an unmeasured conflict must not state a current version it never read");
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
