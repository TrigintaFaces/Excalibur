// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Streaming;

namespace Excalibur.Dispatch.Abstractions.Tests.Streaming;

/// <summary>
/// Unit tests for the <see cref="Chunk{T}"/> readonly record struct.
/// Validates value semantics, deconstruction, and computed properties.
/// </summary>
/// <remarks>
/// Sprint 445 S445.4: Unit tests for streaming helper types.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Streaming")]
public sealed class ChunkShould : UnitTestBase
{
	#region Constructor and Properties

	[Fact]
	public void StoreDataCorrectly()
	{
		// Arrange & Act
		var chunk = new Chunk<string>("test-data", 5, false, false);

		// Assert
		chunk.Data.ShouldBe("test-data");
	}

	[Fact]
	public void StoreIndexCorrectly()
	{
		// Arrange & Act
		var chunk = new Chunk<int>(42, 100, false, false);

		// Assert
		chunk.Index.ShouldBe(100);
	}

	[Fact]
	public void StoreIsFirstCorrectly()
	{
		// Arrange & Act
		var chunk = new Chunk<int>(1, 0, true, false);

		// Assert
		chunk.IsFirst.ShouldBeTrue();
	}

	[Fact]
	public void StoreIsLastCorrectly()
	{
		// Arrange & Act
		var chunk = new Chunk<int>(1, 0, false, true);

		// Assert
		chunk.IsLast.ShouldBeTrue();
	}

	#endregion

	#region Computed Properties - IsMiddle

	[Fact]
	public void ReturnTrueForIsMiddle_WhenNotFirstAndNotLast()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 5, IsFirst: false, IsLast: false);

		// Act & Assert
		chunk.IsMiddle.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForIsMiddle_WhenIsFirst()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 0, IsFirst: true, IsLast: false);

		// Act & Assert
		chunk.IsMiddle.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForIsMiddle_WhenIsLast()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 10, IsFirst: false, IsLast: true);

		// Act & Assert
		chunk.IsMiddle.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForIsMiddle_WhenIsSingle()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 0, IsFirst: true, IsLast: true);

		// Act & Assert
		chunk.IsMiddle.ShouldBeFalse();
	}

	#endregion

	#region Computed Properties - IsSingle

	[Fact]
	public void ReturnTrueForIsSingle_WhenIsFirstAndIsLast()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 0, IsFirst: true, IsLast: true);

		// Act & Assert
		chunk.IsSingle.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForIsSingle_WhenIsFirstOnly()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 0, IsFirst: true, IsLast: false);

		// Act & Assert
		chunk.IsSingle.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForIsSingle_WhenIsLastOnly()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 5, IsFirst: false, IsLast: true);

		// Act & Assert
		chunk.IsSingle.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForIsSingle_WhenIsMiddle()
	{
		// Arrange
		var chunk = new Chunk<int>(42, 3, IsFirst: false, IsLast: false);

		// Act & Assert
		chunk.IsSingle.ShouldBeFalse();
	}

	#endregion

	#region Value Equality

	[Fact]
	public void BeEqual_WhenAllPropertiesMatch()
	{
		// Arrange
		var chunk1 = new Chunk<string>("data", 5, true, false);
		var chunk2 = new Chunk<string>("data", 5, true, false);

		// Act & Assert
		chunk1.ShouldBe(chunk2);
		(chunk1 == chunk2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqual_WhenDataDiffers()
	{
		// Arrange
		var chunk1 = new Chunk<string>("data1", 5, true, false);
		var chunk2 = new Chunk<string>("data2", 5, true, false);

		// Act & Assert
		chunk1.ShouldNotBe(chunk2);
	}

	[Fact]
	public void NotBeEqual_WhenIndexDiffers()
	{
		// Arrange
		var chunk1 = new Chunk<string>("data", 5, true, false);
		var chunk2 = new Chunk<string>("data", 6, true, false);

		// Act & Assert
		chunk1.ShouldNotBe(chunk2);
	}

	[Fact]
	public void NotBeEqual_WhenIsFirstDiffers()
	{
		// Arrange
		var chunk1 = new Chunk<string>("data", 5, true, false);
		var chunk2 = new Chunk<string>("data", 5, false, false);

		// Act & Assert
		chunk1.ShouldNotBe(chunk2);
	}

	[Fact]
	public void NotBeEqual_WhenIsLastDiffers()
	{
		// Arrange
		var chunk1 = new Chunk<string>("data", 5, true, false);
		var chunk2 = new Chunk<string>("data", 5, true, true);

		// Act & Assert
		chunk1.ShouldNotBe(chunk2);
	}

	#endregion

	#region Record Features

	[Fact]
	public void SupportDeconstruction()
	{
		// Arrange
		var chunk = new Chunk<string>("test", 42, true, false);

		// Act
		var (data, index, isFirst, isLast) = chunk;

		// Assert
		data.ShouldBe("test");
		index.ShouldBe(42);
		isFirst.ShouldBeTrue();
		isLast.ShouldBeFalse();
	}

	[Fact]
	public void SupportWithExpression()
	{
		// Arrange
		var original = new Chunk<string>("test", 0, true, false);

		// Act
		var modified = original with { Index = 5, IsLast = true };

		// Assert
		modified.Data.ShouldBe("test");
		modified.Index.ShouldBe(5);
		modified.IsFirst.ShouldBeTrue();
		modified.IsLast.ShouldBeTrue();
	}

	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Arrange & Act
		var defaultChunk = default(Chunk<string>);

		// Assert
		defaultChunk.Data.ShouldBeNull();
		defaultChunk.Index.ShouldBe(0);
		defaultChunk.IsFirst.ShouldBeFalse();
		defaultChunk.IsLast.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectDefaultValuesForValueType()
	{
		// Arrange & Act
		var defaultChunk = default(Chunk<int>);

		// Assert
		defaultChunk.Data.ShouldBe(0);
		defaultChunk.Index.ShouldBe(0);
		defaultChunk.IsFirst.ShouldBeFalse();
		defaultChunk.IsLast.ShouldBeFalse();
	}

	#endregion

	#region Large Index Support

	[Fact]
	public void SupportLargeIndexValues()
	{
		// Arrange & Act
		var chunk = new Chunk<string>("data", long.MaxValue, false, true);

		// Assert
		chunk.Index.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void SupportNegativeIndexValues()
	{
		// Arrange & Act - technically valid, though not recommended
		var chunk = new Chunk<string>("data", -1, false, false);

		// Assert
		chunk.Index.ShouldBe(-1);
	}

	#endregion
}
