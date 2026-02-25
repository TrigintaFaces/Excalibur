// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryCdcOptions"/>.
/// Tests the in-memory CDC options configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HasCorrectDefaults()
	{
		// Arrange & Act
		var options = new InMemoryCdcOptions();

		// Assert
		options.ProcessorId.ShouldBe("inmemory-cdc");
		options.BatchSize.ShouldBe(100);
		options.AutoFlush.ShouldBeTrue();
		options.PreserveHistory.ShouldBeFalse();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ProcessorId_CanBeSet()
	{
		// Arrange
		var options = new InMemoryCdcOptions();

		// Act
		options.ProcessorId = "custom-processor";

		// Assert
		options.ProcessorId.ShouldBe("custom-processor");
	}

	[Fact]
	public void BatchSize_CanBeSet()
	{
		// Arrange
		var options = new InMemoryCdcOptions();

		// Act
		options.BatchSize = 500;

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AutoFlush_CanBeSet()
	{
		// Arrange
		var options = new InMemoryCdcOptions();

		// Act
		options.AutoFlush = false;

		// Assert
		options.AutoFlush.ShouldBeFalse();
	}

	[Fact]
	public void PreserveHistory_CanBeSet()
	{
		// Arrange
		var options = new InMemoryCdcOptions();

		// Act
		options.PreserveHistory = true;

		// Assert
		options.PreserveHistory.ShouldBeTrue();
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_Succeeds_WithValidOptions()
	{
		// Arrange
		var options = new InMemoryCdcOptions
		{
			ProcessorId = "test-processor",
			BatchSize = 50
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ThrowsInvalidOperationException_WhenProcessorIdInvalid(string? invalidId)
	{
		// Arrange
		var options = new InMemoryCdcOptions { ProcessorId = invalidId! };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("ProcessorId");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Validate_ThrowsInvalidOperationException_WhenBatchSizeInvalid(int invalidBatchSize)
	{
		// Arrange
		var options = new InMemoryCdcOptions { BatchSize = invalidBatchSize };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("BatchSize");
	}

	[Fact]
	public void Validate_Succeeds_WithMinimumValidBatchSize()
	{
		// Arrange
		var options = new InMemoryCdcOptions { BatchSize = 1 };

		// Act & Assert - should not throw
		options.Validate();
	}

	#endregion
}
