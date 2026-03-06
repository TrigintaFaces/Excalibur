// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

namespace Excalibur.Data.Tests.CosmosDb.Authorization;

/// <summary>
/// Unit tests for <see cref="CosmosDbActivityGroupGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class CosmosDbActivityGroupGrantStoreShould
{
	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		typeof(CosmosDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(CosmosDbActivityGroupGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIGrantStore()
	{
		typeof(CosmosDbActivityGroupGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void NotImplementIActivityGroupStore()
	{
		typeof(CosmosDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupStore)).ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(CosmosDbActivityGroupGrantStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveExpectedMethodCount()
	{
		// IActivityGroupGrantStore has 4 methods
		var methods = typeof(IActivityGroupGrantStore).GetMethods();
		methods.Length.ShouldBe(4);
	}
}
