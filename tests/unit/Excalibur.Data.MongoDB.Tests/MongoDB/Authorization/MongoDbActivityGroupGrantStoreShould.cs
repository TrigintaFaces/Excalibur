// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Authorization;

namespace Excalibur.Data.Tests.MongoDB.Authorization;

/// <summary>
/// Unit tests for <see cref="MongoDbActivityGroupGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class MongoDbActivityGroupGrantStoreShould
{
	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		typeof(MongoDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(MongoDbActivityGroupGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIGrantStore()
	{
		typeof(MongoDbActivityGroupGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void NotImplementIActivityGroupStore()
	{
		typeof(MongoDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupStore)).ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(MongoDbActivityGroupGrantStore).IsSealed.ShouldBeTrue();
	}
}
