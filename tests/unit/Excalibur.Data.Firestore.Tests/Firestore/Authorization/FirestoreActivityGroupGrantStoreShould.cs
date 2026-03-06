// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Authorization;

namespace Excalibur.Data.Tests.Firestore.Authorization;

/// <summary>
/// Unit tests for <see cref="FirestoreActivityGroupGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class FirestoreActivityGroupGrantStoreShould
{
	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		typeof(FirestoreActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(FirestoreActivityGroupGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIGrantStore()
	{
		typeof(FirestoreActivityGroupGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void NotImplementIActivityGroupStore()
	{
		typeof(FirestoreActivityGroupGrantStore).IsAssignableTo(typeof(IActivityGroupStore)).ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(FirestoreActivityGroupGrantStore).IsSealed.ShouldBeTrue();
	}
}
