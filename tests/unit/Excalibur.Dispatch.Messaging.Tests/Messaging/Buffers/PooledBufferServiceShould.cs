// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

/// <summary>
///     Tests for the <see cref="PooledBufferService" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PooledBufferServiceShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		var sut = new PooledBufferService();
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithSharedPool()
	{
		var sut = new PooledBufferService(useShared: true);
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomPoolParameters()
	{
		var sut = new PooledBufferService(maxArrayLength: 512 * 1024, maxArraysPerBucket: 25, clearBuffersByDefault: false);
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithSharedAndClearingOptions()
	{
		var sut = new PooledBufferService(useShared: true, clearBuffersByDefault: true);
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void RentBufferOfRequestedMinimumSize()
	{
		var sut = new PooledBufferService();
		var buffer = sut.RentBuffer(256);

		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(256);

		sut.ReturnBuffer(buffer);
	}

	[Fact]
	public void ReturnBufferWithoutErrors()
	{
		var sut = new PooledBufferService();
		var buffer = sut.RentBuffer(128);

		Should.NotThrow(() => sut.ReturnBuffer(buffer));
	}

	[Fact]
	public void ImplementIPooledBufferService()
	{
		var sut = new PooledBufferService();
		sut.ShouldBeAssignableTo<IPooledBufferService>();
	}

	[Fact]
	public void TrackStatistics()
	{
		var sut = new PooledBufferService();
		var buffer1 = sut.RentBuffer(100);
		var buffer2 = sut.RentBuffer(200);
		sut.ReturnBuffer(buffer1);
		sut.ReturnBuffer(buffer2);

		var stats = sut.GetStatistics();
		stats.ShouldNotBeNull();
	}

	[Fact]
	public void TrackRentedBufferCount()
	{
		var sut = new PooledBufferService();
		var buffer1 = sut.RentBuffer(100);
		var buffer2 = sut.RentBuffer(200);

		sut.RentedBuffers.ShouldBe(2);

		sut.ReturnBuffer(buffer1);
		sut.RentedBuffers.ShouldBe(1);

		sut.ReturnBuffer(buffer2);
		sut.RentedBuffers.ShouldBe(0);
	}

	[Fact]
	public void TrackTotalOperations()
	{
		var sut = new PooledBufferService();
		var buffer = sut.RentBuffer(100);
		sut.ReturnBuffer(buffer);

		sut.TotalRentOperations.ShouldBe(1);
		sut.TotalReturnOperations.ShouldBe(1);
	}

	[Fact]
	public void RentMultipleBuffersConcurrently()
	{
		var sut = new PooledBufferService(useShared: true);
		var buffers = new byte[10][];

		for (var i = 0; i < 10; i++)
		{
			buffers[i] = sut.RentBuffer(128);
			buffers[i].ShouldNotBeNull();
		}

		for (var i = 0; i < 10; i++)
		{
			sut.ReturnBuffer(buffers[i]);
		}
	}

	[Fact]
	public void HaveSharedInstance()
	{
		PooledBufferService.Shared.ShouldNotBeNull();
		PooledBufferService.Shared.ShouldBeAssignableTo<IPooledBufferService>();
	}

	[Fact]
	public void ThrowForNullBufferOnReturn()
	{
		var sut = new PooledBufferService();
		Should.Throw<ArgumentNullException>(() => sut.ReturnBuffer(null!));
	}

	[Fact]
	public void ThrowForNegativeMinimumLength()
	{
		var sut = new PooledBufferService();
		Should.Throw<ArgumentOutOfRangeException>(() => sut.RentBuffer(-1));
	}
}
