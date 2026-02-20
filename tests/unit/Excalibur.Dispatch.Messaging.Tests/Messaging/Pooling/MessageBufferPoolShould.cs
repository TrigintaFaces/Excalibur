// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Tests.Messaging.Pooling;

/// <summary>
/// Unit tests for <see cref="MessageBufferPool"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pooling")]
[Trait("Priority", "0")]
public sealed class MessageBufferPoolShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithDefaults_CreatesPool()
	{
		// Act
		var pool = new MessageBufferPool();

		// Assert
		_ = pool.ShouldNotBeNull();
		pool.TotalRented.ShouldBe(0);
		pool.TotalReturned.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithCustomParameters_CreatesPool()
	{
		// Act
		var pool = new MessageBufferPool(maxArrayLength: 2048, maxArraysPerBucket: 10);

		// Assert
		_ = pool.ShouldNotBeNull();
	}

	#endregion

	#region Shared Property Tests

	[Fact]
	public void Shared_ReturnsSameInstance()
	{
		// Act
		var shared1 = MessageBufferPool.Shared;
		var shared2 = MessageBufferPool.Shared;

		// Assert
		shared1.ShouldBeSameAs(shared2);
	}

	[Fact]
	public void Shared_IsNotNull()
	{
		// Assert
		_ = MessageBufferPool.Shared.ShouldNotBeNull();
	}

	#endregion

	#region Rent Tests

	[Fact]
	public void Rent_WithPositiveLength_ReturnsRentedBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		using var rented = pool.Rent(100);

		// Assert
		_ = rented.Buffer.ShouldNotBeNull();
		rented.Length.ShouldBe(100);
	}

	[Fact]
	public void Rent_IncrementsRentedCount()
	{
		// Arrange
		var pool = new MessageBufferPool();
		var initialRented = pool.TotalRented;

		// Act
		using var rented = pool.Rent(100);

		// Assert
		pool.TotalRented.ShouldBe(initialRented + 1);
	}

	[Fact]
	public void Rent_WithNegativeLength_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => pool.Rent(-1));
	}

	[Fact]
	public void Rent_WithLengthExceedingMax_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var pool = new MessageBufferPool(maxArrayLength: 1000);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => pool.Rent(2000));
	}

	[Fact]
	public void Rent_WithZeroLength_Succeeds()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		using var rented = pool.Rent(0);

		// Assert
		rented.Length.ShouldBe(0);
	}

	#endregion

	#region Statistics Tests

	[Fact]
	public void TotalRented_IncreasesOnEachRent()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		using var rented1 = pool.Rent(100);
		using var rented2 = pool.Rent(200);
		using var rented3 = pool.Rent(300);

		// Assert
		pool.TotalRented.ShouldBe(3);
	}

	[Fact]
	public void TotalReturned_IncreasesOnEachReturn()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		using (pool.Rent(100)) { }
		using (pool.Rent(200)) { }
		using (pool.Rent(300)) { }

		// Assert
		pool.TotalReturned.ShouldBe(3);
	}

	[Fact]
	public void CurrentlyInUse_ReflectsActiveRentals()
	{
		// Arrange
		var pool = new MessageBufferPool();
		pool.CurrentlyInUse.ShouldBe(0);

		// Act
		var rented1 = pool.Rent(100);
		pool.CurrentlyInUse.ShouldBe(1);

		var rented2 = pool.Rent(200);
		pool.CurrentlyInUse.ShouldBe(2);

		rented1.Dispose();
		pool.CurrentlyInUse.ShouldBe(1);

		rented2.Dispose();
		pool.CurrentlyInUse.ShouldBe(0);
	}

	#endregion

	#region GetOptimalBufferSize Tests

	[Fact]
	public void GetOptimalBufferSize_ForSmallSize_Returns512()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(100);

		// Assert
		size.ShouldBe(512);
	}

	[Fact]
	public void GetOptimalBufferSize_For512_Returns512()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(512);

		// Assert
		size.ShouldBe(512);
	}

	[Fact]
	public void GetOptimalBufferSize_For513_Returns1024()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(513);

		// Assert
		size.ShouldBe(1024);
	}

	[Fact]
	public void GetOptimalBufferSize_For1024_Returns1024()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(1024);

		// Assert
		size.ShouldBe(1024);
	}

	[Fact]
	public void GetOptimalBufferSize_For4096_Returns4096()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(4096);

		// Assert
		size.ShouldBe(4096);
	}

	[Fact]
	public void GetOptimalBufferSize_ForLargeSize_RoundsUpToPowerOfTwo()
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(200000);

		// Assert
		// Should be 262144 (2^18 = 262144 which is the next power of 2 after 200000)
		size.ShouldBe(262144);
	}

	[Theory]
	[InlineData(1, 512)]
	[InlineData(511, 512)]
	[InlineData(512, 512)]
	[InlineData(1000, 1024)]
	[InlineData(1025, 4096)]
	[InlineData(4000, 4096)]
	[InlineData(4097, 16384)]
	[InlineData(16384, 16384)]
	[InlineData(16385, 65536)]
	[InlineData(65536, 65536)]
	[InlineData(65537, 131072)]
	[InlineData(131072, 131072)]
	public void GetOptimalBufferSize_ReturnsCorrectSize(int requested, int expected)
	{
		// Act
		var size = MessageBufferPool.GetOptimalBufferSize(requested);

		// Assert
		size.ShouldBe(expected);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task Rent_AndReturn_ThreadSafe()
	{
		// Arrange
		var pool = new MessageBufferPool();
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
		const int iterations = 100;
		const int threads = 10;

		// Act
		var tasks = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
		{
			try
			{
				for (var i = 0; i < iterations; i++)
				{
					using var rented = pool.Rent(100 + i);
					// Simulate some work
					rented.Span[0] = 0xFF;
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty();
		pool.TotalRented.ShouldBe(threads * iterations);
		pool.TotalReturned.ShouldBe(threads * iterations);
		pool.CurrentlyInUse.ShouldBe(0);
	}

	#endregion

	#region Buffer Reuse Tests

	[Fact]
	public void Rent_AfterReturn_MayReuseBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act - Rent and return several times
		byte[]? firstBuffer;
		using (var rented1 = pool.Rent(100))
		{
			firstBuffer = rented1.Buffer;
		}

		// Rent again - may get same buffer back
		using var rented2 = pool.Rent(100);

		// Assert - Either reused or new, but statistics should be correct
		pool.TotalRented.ShouldBe(2);
		pool.TotalReturned.ShouldBe(1);
	}

	#endregion
}
