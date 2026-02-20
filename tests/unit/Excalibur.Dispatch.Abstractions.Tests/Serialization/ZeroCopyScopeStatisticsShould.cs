// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="ZeroCopyScopeStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class ZeroCopyScopeStatisticsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_DeserializationCountIsZero()
	{
		// Arrange & Act
		var stats = new ZeroCopyScopeStatistics();

		// Assert
		stats.DeserializationCount.ShouldBe(0);
	}

	[Fact]
	public void Default_BytesProcessedIsZero()
	{
		// Arrange & Act
		var stats = new ZeroCopyScopeStatistics();

		// Assert
		stats.BytesProcessed.ShouldBe(0);
	}

	[Fact]
	public void Default_BufferReusesIsZero()
	{
		// Arrange & Act
		var stats = new ZeroCopyScopeStatistics();

		// Assert
		stats.BufferReuses.ShouldBe(0);
	}

	[Fact]
	public void Default_CurrentBufferSizeIsZero()
	{
		// Arrange & Act
		var stats = new ZeroCopyScopeStatistics();

		// Assert
		stats.CurrentBufferSize.ShouldBe(0);
	}

	#endregion

	#region Property Init Tests

	[Fact]
	public void DeserializationCount_CanBeSet()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { DeserializationCount = 1000 };

		// Assert
		stats.DeserializationCount.ShouldBe(1000);
	}

	[Fact]
	public void BytesProcessed_CanBeSet()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { BytesProcessed = 1024 * 1024 };

		// Assert
		stats.BytesProcessed.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void BufferReuses_CanBeSet()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { BufferReuses = 500 };

		// Assert
		stats.BufferReuses.ShouldBe(500);
	}

	[Fact]
	public void CurrentBufferSize_CanBeSet()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { CurrentBufferSize = 4096 };

		// Assert
		stats.CurrentBufferSize.ShouldBe(4096);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics
		{
			DeserializationCount = 100,
			BytesProcessed = 50000,
			BufferReuses = 80,
			CurrentBufferSize = 8192,
		};

		// Assert
		stats.DeserializationCount.ShouldBe(100);
		stats.BytesProcessed.ShouldBe(50000);
		stats.BufferReuses.ShouldBe(80);
		stats.CurrentBufferSize.ShouldBe(8192);
	}

	#endregion

	#region Large Value Tests

	[Fact]
	public void DeserializationCount_CanStoreLargeValue()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { DeserializationCount = long.MaxValue };

		// Assert
		stats.DeserializationCount.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void BytesProcessed_CanStoreLargeValue()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { BytesProcessed = long.MaxValue };

		// Assert
		stats.BytesProcessed.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void BufferReuses_CanStoreLargeValue()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { BufferReuses = long.MaxValue };

		// Assert
		stats.BufferReuses.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void CurrentBufferSize_CanStoreMaxIntValue()
	{
		// Act
		var stats = new ZeroCopyScopeStatistics { CurrentBufferSize = int.MaxValue };

		// Assert
		stats.CurrentBufferSize.ShouldBe(int.MaxValue);
	}

	#endregion
}
