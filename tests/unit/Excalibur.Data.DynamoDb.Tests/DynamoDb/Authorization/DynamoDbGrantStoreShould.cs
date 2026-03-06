// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Authorization;

/// <summary>
/// Unit tests for <see cref="DynamoDbGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class DynamoDbGrantStoreShould
{
	private static IOptions<DynamoDbAuthorizationOptions> CreateOptions() =>
		Microsoft.Extensions.Options.Options.Create(new DynamoDbAuthorizationOptions
		{
			Region = "us-east-1"
		});

	private static DynamoDbGrantStore CreateStore() =>
		new(A.Fake<IAmazonDynamoDB>(), CreateOptions(), NullLogger<DynamoDbGrantStore>.Instance);

	[Fact]
	public void ImplementIGrantStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IGrantStore>();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IGrantQueryStore>();
	}

	[Fact]
	public void NotImplementIActivityGroupGrantStore()
	{
		typeof(DynamoDbGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbGrantStore(
				(IAmazonDynamoDB)null!,
				CreateOptions(),
				NullLogger<DynamoDbGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbGrantStore(null!, NullLogger<DynamoDbGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DynamoDbGrantStore(
				A.Fake<IAmazonDynamoDB>(),
				CreateOptions(),
				null!));
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIGrantQueryStore()
	{
		var store = CreateStore();

		var result = store.GetService(typeof(IGrantQueryStore));

		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsUnsupportedType()
	{
		var store = CreateStore();

		var result = store.GetService(typeof(IDisposable));

		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		var store = CreateStore();

		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}

	[Fact]
	public void BeSealed()
	{
		typeof(DynamoDbGrantStore).IsSealed.ShouldBeTrue();
	}
}
