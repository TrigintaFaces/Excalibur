// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Firestore.Authorization;

/// <summary>
/// Unit tests for <see cref="FirestoreGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class FirestoreGrantStoreShould
{
	[Fact]
	public void ImplementIGrantStore()
	{
		typeof(FirestoreGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		typeof(FirestoreGrantStore).IsAssignableTo(typeof(IGrantQueryStore)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIActivityGroupGrantStore()
	{
		typeof(FirestoreGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(FirestoreGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new FirestoreGrantStore(null!, NullLogger<FirestoreGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new FirestoreAuthorizationOptions
		{
			ProjectId = "test-project"
		});

		Should.Throw<ArgumentNullException>(() =>
			new FirestoreGrantStore(options, null!));
	}

	[Fact]
	public void HaveGetServiceMethod()
	{
		var method = typeof(FirestoreGrantStore).GetMethod("GetService", [typeof(Type)]);

		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(object));
	}

	[Fact]
	public void BeSealed()
	{
		typeof(FirestoreGrantStore).IsSealed.ShouldBeTrue();
	}
}
