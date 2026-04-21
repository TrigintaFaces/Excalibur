// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class InMemoryIdentityMapStoreShould
{
	private readonly IIdentityMapStore _store;

	public InMemoryIdentityMapStoreShould()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		var provider = services.BuildServiceProvider();
		_store = provider.GetRequiredService<IIdentityMapStore>();
	}

	#region ResolveAsync

	[Fact]
	public async Task ResolveAsync_ReturnNull_WhenNoMappingExists()
	{
		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task ResolveAsync_ReturnAggregateId_WhenMappingExists()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		result.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task ResolveAsync_BeCaseInsensitive()
	{
		await _store.BindAsync("SAP", "ext-001", "Order", "AGG-001", CancellationToken.None);

		var result = await _store.ResolveAsync("sap", "EXT-001", "order", CancellationToken.None);

		result.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task ResolveAsync_DistinguishDifferentExternalSystems()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-SAP", CancellationToken.None);
		await _store.BindAsync("Legacy", "EXT-001", "Order", "AGG-Legacy", CancellationToken.None);

		var sapResult = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		var legacyResult = await _store.ResolveAsync("Legacy", "EXT-001", "Order", CancellationToken.None);

		sapResult.ShouldBe("AGG-SAP");
		legacyResult.ShouldBe("AGG-Legacy");
	}

	[Fact]
	public async Task ResolveAsync_DistinguishDifferentAggregateTypes()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-Order", CancellationToken.None);
		await _store.BindAsync("SAP", "EXT-001", "Account", "AGG-Account", CancellationToken.None);

		var orderResult = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		var accountResult = await _store.ResolveAsync("SAP", "EXT-001", "Account", CancellationToken.None);

		orderResult.ShouldBe("AGG-Order");
		accountResult.ShouldBe("AGG-Account");
	}

	#endregion

	#region BindAsync

	[Fact]
	public async Task BindAsync_CreateNewMapping()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		result.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task BindAsync_UpdateExistingMapping()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-002", CancellationToken.None);

		var result = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		result.ShouldBe("AGG-002");
	}

	#endregion

	#region TryBindAsync

	[Fact]
	public async Task TryBindAsync_CreateNewMapping_WhenNoneExists()
	{
		var result = await _store.TryBindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		result.WasCreated.ShouldBeTrue();
		result.AggregateId.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task TryBindAsync_ReturnExisting_WhenMappingExists()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var result = await _store.TryBindAsync("SAP", "EXT-001", "Order", "AGG-NEW", CancellationToken.None);

		result.WasCreated.ShouldBeFalse();
		result.AggregateId.ShouldBe("AGG-001");
	}

	#endregion

	#region UnbindAsync

	[Fact]
	public async Task UnbindAsync_ReturnTrue_WhenMappingRemoved()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var removed = await _store.UnbindAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		removed.ShouldBeTrue();
		var resolved = await _store.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		resolved.ShouldBeNull();
	}

	[Fact]
	public async Task UnbindAsync_ReturnFalse_WhenNoMappingExists()
	{
		var removed = await _store.UnbindAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		removed.ShouldBeFalse();
	}

	#endregion

	#region ResolveBatchAsync

	[Fact]
	public async Task ResolveBatchAsync_ReturnOnlyExistingMappings()
	{
		await _store.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		await _store.BindAsync("SAP", "EXT-003", "Order", "AGG-003", CancellationToken.None);

		var result = await _store.ResolveBatchAsync(
			"SAP",
			["EXT-001", "EXT-002", "EXT-003"],
			"Order",
			CancellationToken.None);

		result.Count.ShouldBe(2);
		result["EXT-001"].ShouldBe("AGG-001");
		result["EXT-003"].ShouldBe("AGG-003");
		result.ContainsKey("EXT-002").ShouldBeFalse();
	}

	[Fact]
	public async Task ResolveBatchAsync_ReturnEmpty_WhenNoneMapped()
	{
		var result = await _store.ResolveBatchAsync(
			"SAP", ["EXT-001", "EXT-002"], "Order", CancellationToken.None);

		result.ShouldBeEmpty();
	}

	#endregion

	#region Concurrent Access

	[Fact]
	public async Task HandleConcurrentBindAndResolve()
	{
		var tasks = new List<Task>();

		for (var i = 0; i < 100; i++)
		{
			var id = i.ToString();
			tasks.Add(Task.Run(async () =>
			{
				await _store.BindAsync("SAP", $"EXT-{id}", "Order", $"AGG-{id}", CancellationToken.None);
				var result = await _store.ResolveAsync("SAP", $"EXT-{id}", "Order", CancellationToken.None);
				result.ShouldBe($"AGG-{id}");
			}));
		}

		await Task.WhenAll(tasks);
	}

	#endregion
}
