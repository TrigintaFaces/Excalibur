// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchDispatchResultShould
{
	[Fact]
	public void Constructor_SetAllFields()
	{
		// Act
		var result = new BatchDispatchResult(successful: 10, failed: 2, totalLatencyUs: 5000.0);

		// Assert
		result.SuccessfulCount.ShouldBe(10);
		result.FailedCount.ShouldBe(2);
		result.TotalLatencyUs.ShouldBe(5000.0);
	}

	[Fact]
	public void AverageLatencyUs_CalculateCorrectly()
	{
		// Arrange
		var result = new BatchDispatchResult(successful: 8, failed: 2, totalLatencyUs: 1000.0);

		// Act
		var average = result.AverageLatencyUs;

		// Assert — 1000 / (8+2) = 100
		average.ShouldBe(100.0);
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var result1 = new BatchDispatchResult(5, 1, 300.0);
		var result2 = new BatchDispatchResult(5, 1, 300.0);

		// Assert
		result1.ShouldBe(result2);
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_DifferentValues_AreNotEqual()
	{
		// Arrange
		var result1 = new BatchDispatchResult(5, 1, 300.0);
		var result2 = new BatchDispatchResult(5, 2, 300.0);

		// Assert
		result1.ShouldNotBe(result2);
		(result1 != result2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameValues_ReturnSameHash()
	{
		// Arrange
		var result1 = new BatchDispatchResult(5, 1, 300.0);
		var result2 = new BatchDispatchResult(5, 1, 300.0);

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnFalseForNonBatchResult()
	{
		// Arrange
		var result = new BatchDispatchResult(5, 1, 300.0);

		// Act & Assert
		result.Equals("not a batch result").ShouldBeFalse();
	}
}
