// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.CosmosDb.Authorization;

/// <summary>
/// Unit tests for <see cref="CosmosDbGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class CosmosDbGrantStoreShould
{
	[Fact]
	public void ImplementIGrantStore()
	{
		typeof(CosmosDbGrantStore).IsAssignableTo(typeof(IGrantStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		typeof(CosmosDbGrantStore).IsAssignableTo(typeof(IGrantQueryStore)).ShouldBeTrue();
	}

	[Fact]
	public void NotImplementIActivityGroupGrantStore()
	{
		typeof(CosmosDbGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(CosmosDbGrantStore).IsAssignableTo(typeof(IAsyncDisposable)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbGrantStore(null!, NullLogger<CosmosDbGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CosmosDbAuthorizationOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;",
			DatabaseName = "testdb",
			GrantsContainerName = "grants"
		});

		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbGrantStore(options, null!));
	}

	[Fact]
	public void HaveGetServiceMethod()
	{
		var method = typeof(CosmosDbGrantStore).GetMethod("GetService", [typeof(Type)]);

		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(object));
	}

	[Fact]
	public void BeSealed()
	{
		typeof(CosmosDbGrantStore).IsSealed.ShouldBeTrue();
	}
}
