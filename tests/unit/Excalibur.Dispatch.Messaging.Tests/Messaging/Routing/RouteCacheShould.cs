// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
///     Tests for the <see cref="RouteCache" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RouteCacheShould
{
	[Fact]
	public void CreateWithCapacity()
	{
		var sut = new RouteCache(16);
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnFalseForMissingKey()
	{
		var sut = new RouteCache(16);
		sut.TryGetRoute(42, out var routeId).ShouldBeFalse();
		routeId.ShouldBe(-1);
	}

	[Fact]
	public void StoreAndRetrieveRoute()
	{
		var sut = new RouteCache(16);
		sut.AddRoute(42, 7);

		sut.TryGetRoute(42, out var routeId).ShouldBeTrue();
		routeId.ShouldBe(7);
	}

	[Fact]
	public void OverwriteExistingRoute()
	{
		var sut = new RouteCache(16);
		sut.AddRoute(42, 7);
		sut.AddRoute(42, 99);

		sut.TryGetRoute(42, out var routeId).ShouldBeTrue();
		routeId.ShouldBe(99);
	}

	[Fact]
	public void ClearAllRoutes()
	{
		var sut = new RouteCache(16);
		sut.AddRoute(1, 10);
		sut.AddRoute(2, 20);

		sut.Clear();

		sut.TryGetRoute(1, out _).ShouldBeFalse();
		sut.TryGetRoute(2, out _).ShouldBeFalse();
	}

	[Fact]
	public void HandleNonPowerOfTwoCapacity()
	{
		// Capacity 10 should round up to 16 internally
		var sut = new RouteCache(10);
		sut.AddRoute(5, 50);

		sut.TryGetRoute(5, out var routeId).ShouldBeTrue();
		routeId.ShouldBe(50);
	}

	[Fact]
	public void HandleMultipleDistinctKeys()
	{
		var sut = new RouteCache(64);
		for (var i = 0; i < 32; i++)
		{
			sut.AddRoute(i, i * 10);
		}

		for (var i = 0; i < 32; i++)
		{
			sut.TryGetRoute(i, out var routeId).ShouldBeTrue();
			routeId.ShouldBe(i * 10);
		}
	}
}
