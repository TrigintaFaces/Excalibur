// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="CacheAlignedUtilities"/>.
/// </summary>
/// <remarks>
/// Note: The AllocateAligned/FreeAligned methods have a known limitation where
/// FreeAligned doesn't track the original pointer. Tests avoid calling FreeHGlobal
/// on aligned pointers to prevent heap corruption. See source comments for details.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class CacheAlignedUtilitiesShould : UnitTestBase
{
	private const int CacheLineSize = 64;

	#region AllocateAligned Tests

	[Fact]
	public void AllocateNonNullPointer()
	{
		// Act
		var ptr = CacheAlignedUtilities.AllocateAligned(size: 64);

		// Assert
		// Note: We don't free the pointer because AllocateAligned returns an aligned offset
		// into the allocated memory, not the original allocation pointer. Freeing the aligned
		// pointer would cause heap corruption. This is a known limitation documented in the source.
		ptr.ShouldNotBe(IntPtr.Zero);
	}

	[Fact]
	public void AllocateAlignedToCacheLine()
	{
		// Act
		var ptr = CacheAlignedUtilities.AllocateAligned(size: 128);

		// Assert - Check alignment to 64-byte boundary
		var address = ptr.ToInt64();
		(address % CacheLineSize).ShouldBe(0);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(32)]
	[InlineData(64)]
	[InlineData(128)]
	[InlineData(256)]
	[InlineData(1024)]
	public void AllocateAlignedForVariousSizes(int size)
	{
		// Act
		var ptr = CacheAlignedUtilities.AllocateAligned(size);

		// Assert - All allocations should be cache-aligned
		var address = ptr.ToInt64();
		(address % CacheLineSize).ShouldBe(0);
	}

	[Fact]
	public void AllocateZeroSize()
	{
		// Act & Assert - Zero size should still return aligned pointer
		var ptr = CacheAlignedUtilities.AllocateAligned(size: 0);

		ptr.ShouldNotBe(IntPtr.Zero);
		var address = ptr.ToInt64();
		(address % CacheLineSize).ShouldBe(0);
	}

	[Fact]
	public void AllocateDifferentPointersForMultipleCalls()
	{
		// Act
		var ptr1 = CacheAlignedUtilities.AllocateAligned(size: 64);
		var ptr2 = CacheAlignedUtilities.AllocateAligned(size: 64);

		// Assert - Different allocations should return different pointers
		ptr1.ShouldNotBe(ptr2);
	}

	#endregion

	#region FreeAligned Tests

	[Fact]
	public void FreeAlignedNotThrowForValidPointer()
	{
		// Arrange - Use a pointer from AllocHGlobal directly (not from AllocateAligned)
		// because FreeAligned calls Marshal.FreeHGlobal internally
		var ptr = Marshal.AllocHGlobal(128);

		// Act & Assert - Should not throw for a properly allocated pointer
		Should.NotThrow(() => CacheAlignedUtilities.FreeAligned(ptr));
	}

	#endregion

	#region ValidateAlignment Tests

	[Fact]
	public void ValidateAlignmentNotThrowForProperlyAlignedType()
	{
		// Act & Assert - Should not throw for CacheAlignedTimestamp which is 64 bytes
		// Note: ValidateAlignment has [Conditional("DEBUG")] attribute, so we call it directly
		CacheAlignedUtilities.ValidateAlignment<CacheAlignedTimestamp>();
		// If we get here without assertion failure in DEBUG mode, the test passes
	}

	[Fact]
	public void ValidateAlignmentNotThrowForCacheAlignedCounter()
	{
		// Act & Assert - CacheAlignedCounter should be 64 bytes
		// Note: ValidateAlignment has [Conditional("DEBUG")] attribute, so we call it directly
		CacheAlignedUtilities.ValidateAlignment<CacheAlignedCounter>();
		// If we get here without assertion failure in DEBUG mode, the test passes
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void AllocateLargeSize()
	{
		// Arrange
		const int largeSize = 10_000;

		// Act
		var ptr = CacheAlignedUtilities.AllocateAligned(largeSize);

		// Assert
		ptr.ShouldNotBe(IntPtr.Zero);
		var address = ptr.ToInt64();
		(address % CacheLineSize).ShouldBe(0);
	}

	[Fact]
	public void AllocateMultipleTimesInSequence()
	{
		// Arrange
		var pointers = new List<IntPtr>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			var ptr = CacheAlignedUtilities.AllocateAligned(size: 64);
			pointers.Add(ptr);
		}

		// Assert - All pointers should be aligned
		foreach (var ptr in pointers)
		{
			var address = ptr.ToInt64();
			(address % CacheLineSize).ShouldBe(0);
		}
	}

	#endregion
}
