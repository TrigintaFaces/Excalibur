// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

namespace Excalibur.Data.Tests.DynamoDb.Authorization;

/// <summary>
/// Unit tests for <see cref="DynamoDbActivityGroupGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class DynamoDbActivityGroupGrantStoreShould
{
	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		typeof(DynamoDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(DynamoDbActivityGroupGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIGrantStore()
	{
		typeof(DynamoDbActivityGroupGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void NotImplementIActivityGroupStore()
	{
		typeof(DynamoDbActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupStore)).ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(DynamoDbActivityGroupGrantStore).IsSealed.ShouldBeTrue();
	}
}
