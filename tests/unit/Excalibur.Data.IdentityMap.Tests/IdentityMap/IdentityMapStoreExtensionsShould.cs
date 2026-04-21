// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class IdentityMapStoreExtensionsShould
{
	private readonly IIdentityMapStore _store;

	public IdentityMapStoreExtensionsShould()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		_store = services.BuildServiceProvider().GetRequiredService<IIdentityMapStore>();
	}

	#region Typed ResolveAsync

	[Fact]
	public async Task ResolveAsync_ReturnTypedGuid()
	{
		var id = Guid.NewGuid();
		await _store.BindAsync("SAP", "EXT-001", "Order", id.ToString(), CancellationToken.None);

		var result = await _store.ResolveAsync<Guid>("SAP", "EXT-001", "Order", CancellationToken.None);

		result.ShouldBe(id);
	}

	[Fact]
	public async Task ResolveAsync_ReturnTypedInt()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "42", CancellationToken.None);

		var result = await _store.ResolveAsync<int>("SAP", "EXT-001", "Order", CancellationToken.None);

		result.ShouldBe(42);
	}

	[Fact]
	public async Task ResolveAsync_ReturnNull_WhenNoMapping()
	{
		var result = await _store.ResolveAsync<Guid>("SAP", "EXT-NONE", "Order", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task ResolveAsync_ThrowOnNullStore()
	{
		IIdentityMapStore store = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.ResolveAsync<Guid>("SAP", "EXT-001", "Order", CancellationToken.None));
	}

	#endregion

	#region Typed BindAsync

	[Fact]
	public async Task BindAsync_WithTypedKey()
	{
		var id = Guid.NewGuid();
		await _store.BindAsync("SAP", "EXT-001", "Order", id, CancellationToken.None);

		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		result.ShouldBe(id.ToString());
	}

	[Fact]
	public async Task BindAsync_WithIntKey()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", 42, CancellationToken.None);

		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		result.ShouldBe("42");
	}

	[Fact]
	public async Task BindAsync_ThrowOnNullStore()
	{
		IIdentityMapStore store = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.BindAsync("SAP", "EXT-001", "Order", Guid.NewGuid(), CancellationToken.None));
	}

	#endregion

	#region Typed TryBindAsync

	[Fact]
	public async Task TryBindAsync_WithTypedKey()
	{
		var id = Guid.NewGuid();
		var result = await _store.TryBindAsync("SAP", "EXT-001", "Order", id, CancellationToken.None);

		result.WasCreated.ShouldBeTrue();
		result.AggregateId.ShouldBe(id.ToString());
	}

	[Fact]
	public async Task TryBindAsync_ThrowOnNullStore()
	{
		IIdentityMapStore store = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.TryBindAsync("SAP", "EXT-001", "Order", Guid.NewGuid(), CancellationToken.None));
	}

	#endregion
}
