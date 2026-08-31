// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// A decorated projection store must not hide a capability the store beneath it genuinely provides,
/// and the capability it exposes must still uphold the decorator's invariant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionStoreCapabilityForwardingShould
{
	private const string Tenant = "tenant-a";

	private static readonly byte[] Plaintext = [7, 7, 7];

	[Fact]
	public async Task ExposePaginationOfTheStoreItDecorates()
	{
		var inner = new CapablePageableStore();
		var decorated = Decorate(inner);

		var capability = decorated.GetService(typeof(IPageableProjectionStore<TestProjection>));
		capability.ShouldNotBeNull("the decorated store implements pagination, so the decorator must expose it");

		var page = await ((IPageableProjectionStore<TestProjection>)capability)
			.QueryPagedAsync(null, 1, 10, null, TestContext.Current.CancellationToken);

		// The capability must actually work, not merely resolve.
		page.Items.Count.ShouldBe(1);
		page.Items[0].Id.ShouldBe("from-inner");
		inner.PagedCalls.ShouldBe(1);
	}

	[Fact]
	public async Task ExposeCursorPaginationOfTheStoreItDecorates()
	{
		var inner = new CapableCursorStore();
		var decorated = Decorate(inner);

		var capability = decorated.GetService(typeof(ICursorProjectionStore<TestProjection>));
		capability.ShouldNotBeNull("the decorated store implements cursor pagination, so the decorator must expose it");

		var page = await ((ICursorProjectionStore<TestProjection>)capability)
			.QueryCursorAsync(null, null, 10, TestContext.Current.CancellationToken);

		page.Items.Single().Id.ShouldBe("from-inner");
		page.NextCursor.ShouldBe("next");
		inner.CursorCalls.ShouldBe(1);
	}

	[Fact]
	public async Task ReachPaginationThroughTheExtensionMethodConsumersActuallyCall()
	{
		var inner = new CapablePageableStore();

		var page = await Decorate(inner).QueryPagedAsync(null, 1, 10, null, TestContext.Current.CancellationToken);

		// If the capability were hidden, this would silently page QueryAsync in memory instead.
		inner.PagedCalls.ShouldBe(1, "the provider's native paging must be used, not the in-memory fallback");
		inner.QueryCalls.ShouldBe(0);
		page.Items[0].Id.ShouldBe("from-inner");
	}

	[Fact]
	public void NotAdvertiseACapabilityTheDecoratedStoreLacks()
	{
		// The inverse failure: a decorator must never claim a capability its inner store cannot perform.
		var decorated = Decorate(new PlainStore());

		decorated.GetService(typeof(IPageableProjectionStore<TestProjection>)).ShouldBeNull();
		decorated.GetService(typeof(ICursorProjectionStore<TestProjection>)).ShouldBeNull();
	}

	[Fact]
	public async Task StillFailClosedOnTheCapabilityWhenNoTenantIsAmbient()
	{
		// The forwarded capability must carry the decorator's invariant, not bypass it.
		var context = new MutableTenantContext { TenantId = null };
		var inner = new CapablePageableStore();
		IProjectionStore<TestProjection> decorated = new TenantScopedProjectionStore<TestProjection>(inner, context);

		var capability = (IPageableProjectionStore<TestProjection>)
			decorated.GetService(typeof(IPageableProjectionStore<TestProjection>))!;

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await capability.QueryPagedAsync(null, 1, 10, null, TestContext.Current.CancellationToken));

		inner.PagedCalls.ShouldBe(0, "the unscoped call must never reach the store");
	}

	[Fact]
	public async Task RouteTheCapabilityViewsOwnBaseSurfaceBackThroughTheDecorator()
	{
		// A capability interface derives from IProjectionStore, so the view is itself a projection store.
		// It must not become an unmediated handle on the store beneath the decorator.
		var context = new MutableTenantContext { TenantId = null };
		var inner = new CapablePageableStore();
		IProjectionStore<TestProjection> decorated = new TenantScopedProjectionStore<TestProjection>(inner, context);

		var view = (IProjectionStore<TestProjection>)
			decorated.GetService(typeof(IPageableProjectionStore<TestProjection>))!;

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await view.GetByIdAsync("id", TestContext.Current.CancellationToken));
	}

	// -----------------------------------------------------------------------------------------------
	// EncryptingProjectionStoreDecorator. Every projection it returns has to pass through decryption,
	// so a capability handed over unwrapped would return the stored ciphertext.
	// -----------------------------------------------------------------------------------------------

	[Fact]
	public void ExposePaginationThroughTheEncryptingDecorator()
	{
		var decorated = Encrypting(new CapablePageableStore(), A.Fake<IEncryptionProvider>());

		decorated.GetService(typeof(IPageableProjectionStore<TestProjection>))
			.ShouldNotBeNull("the decorated store implements pagination, so the decorator must expose it");
	}

	[Fact]
	public void ExposeCursorPaginationThroughTheEncryptingDecorator()
	{
		var decorated = Encrypting(new CapableCursorStore(), A.Fake<IEncryptionProvider>());

		decorated.GetService(typeof(ICursorProjectionStore<TestProjection>)).ShouldNotBeNull();
	}

	[Fact]
	public void NotAdvertiseACapabilityTheEncryptedStoreLacks()
	{
		// The inverse failure: a decorator must never claim a capability its inner store cannot perform.
		var decorated = Encrypting(new PlainStore(), A.Fake<IEncryptionProvider>());

		decorated.GetService(typeof(IPageableProjectionStore<TestProjection>)).ShouldBeNull();
		decorated.GetService(typeof(ICursorProjectionStore<TestProjection>)).ShouldBeNull();
	}

	[Fact]
	public async Task EncryptWritesMadeThroughTheEncryptingCapabilityView()
	{
		// The capability derives from IProjectionStore, so the view can be written through. Handing back
		// the bare inner store would put the plaintext straight into storage.
		var inner = new CapablePageableStore();
		var view = (IProjectionStore<TestProjection>)Encrypting(inner, EncryptingProvider())
			.GetService(typeof(IPageableProjectionStore<TestProjection>))!;

		await view.UpsertAsync(
			"id", new TestProjection { Id = "id", Secret = Plaintext }, TestContext.Current.CancellationToken);

		var stored = inner.Upserted.ShouldNotBeNull();
		stored.Secret.ShouldNotBe(Plaintext, "the store must never receive the plaintext");
		EncryptedData.IsFieldEncrypted(stored.Secret)
			.ShouldBeTrue("the field that reached storage must be a ciphertext envelope");
	}

	[Fact]
	public async Task DecryptThePageReturnedThroughTheEncryptingCapabilityView()
	{
		// Write through the capability view, then read back through it. A view that forwarded the inner
		// capability raw would hand the caller the ciphertext it just wrote.
		var inner = new CapablePageableStore();
		var decorated = Encrypting(inner, EncryptingProvider());

		await decorated.UpsertAsync(
			"id", new TestProjection { Id = "id", Secret = Plaintext }, TestContext.Current.CancellationToken);
		inner.Stored = inner.Upserted;
		EncryptedData.IsFieldEncrypted(inner.Stored!.Secret).ShouldBeTrue("the fixture must hold ciphertext");

		var capability = (IPageableProjectionStore<TestProjection>)
			decorated.GetService(typeof(IPageableProjectionStore<TestProjection>))!;

		var page = await capability.QueryPagedAsync(null, 1, 10, null, TestContext.Current.CancellationToken);

		page.Items.ShouldHaveSingleItem().Secret.ShouldBe(Plaintext, "the view must decrypt the page it produces");
	}

	[Fact]
	public async Task DecryptTheCursorPageReturnedThroughTheEncryptingCapabilityView()
	{
		// The cursor view is a second, separately-wired view over the same decorator, so it needs its own arm.
		var inner = new CapableCursorStore();
		var decorated = Encrypting(inner, EncryptingProvider());

		await decorated.UpsertAsync(
			"id", new TestProjection { Id = "id", Secret = Plaintext }, TestContext.Current.CancellationToken);
		inner.Stored = inner.Upserted;
		EncryptedData.IsFieldEncrypted(inner.Stored!.Secret).ShouldBeTrue("the fixture must hold ciphertext");

		var capability = (ICursorProjectionStore<TestProjection>)
			decorated.GetService(typeof(ICursorProjectionStore<TestProjection>))!;

		var page = await capability.QueryCursorAsync(null, null, 10, TestContext.Current.CancellationToken);

		page.Items.ShouldHaveSingleItem().Secret.ShouldBe(Plaintext, "the view must decrypt the page it produces");
	}

	private static IProjectionStore<TestProjection> Encrypting(
		IProjectionStore<TestProjection> inner,
		IEncryptionProvider provider)
	{
		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.GetPrimary()).Returns(provider);
		_ = A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);

		return new EncryptingProjectionStoreDecorator<TestProjection>(
			inner, registry, Options.Create(new EncryptionOptions { Mode = EncryptionMode.EncryptAndDecrypt }));
	}

	/// <summary>A provider that round-trips the one plaintext these locks use.</summary>
	private static IEncryptionProvider EncryptingProvider()
	{
		var envelope = new EncryptedData
		{
			Ciphertext = [9, 9, 9],
			KeyId = "key-1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
		};

		var provider = A.Fake<IEncryptionProvider>();
		_ = A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(envelope);
		_ = A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Plaintext);

		return provider;
	}

	private static IProjectionStore<TestProjection> Decorate(IProjectionStore<TestProjection> inner) =>
		new TenantScopedProjectionStore<TestProjection>(inner, new MutableTenantContext { TenantId = Tenant });

	internal sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;

		/// <summary>A field the encrypting decorator protects, so its ciphertext is visible to a test.</summary>
		[EncryptedField]
		public byte[]? Secret { get; set; }
	}

	private class PlainStore : IProjectionStore<TestProjection>
	{
		public int QueryCalls { get; private set; }

		/// <summary>The projection this store was actually handed, so a caller can see what reached storage.</summary>
		public TestProjection? Upserted { get; private set; }

		public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult<TestProjection?>(new TestProjection { Id = id });

		public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken)
		{
			Upserted = projection;
			return Task.CompletedTask;
		}

		public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task<IReadOnlyList<TestProjection>> QueryAsync(
			IDictionary<string, object>? filters,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			QueryCalls++;
			return Task.FromResult<IReadOnlyList<TestProjection>>([new TestProjection { Id = "from-query" }]);
		}

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult(1L);
	}

	private sealed class CapablePageableStore : PlainStore, IPageableProjectionStore<TestProjection>
	{
		public int PagedCalls { get; private set; }

		/// <summary>The row this store holds, as it holds it. Left null to keep the default fixture row.</summary>
		public TestProjection? Stored { get; set; }

		public Task<PagedResult<TestProjection>> QueryPagedAsync(
			IDictionary<string, object>? filters,
			int pageNumber,
			int pageSize,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			PagedCalls++;
			return Task.FromResult(new PagedResult<TestProjection>(
				[Stored ?? new TestProjection { Id = "from-inner" }],
				pageNumber,
				pageSize,
				1));
		}
	}

	private sealed class CapableCursorStore : PlainStore, ICursorProjectionStore<TestProjection>
	{
		public int CursorCalls { get; private set; }

		/// <summary>The row this store holds, as it holds it. Left null to keep the default fixture row.</summary>
		public TestProjection? Stored { get; set; }

		public Task<CursorPagedResult<TestProjection>> QueryCursorAsync(
			IDictionary<string, object>? filters,
			string? cursor,
			int pageSize,
			CancellationToken cancellationToken)
		{
			CursorCalls++;
			return Task.FromResult(new CursorPagedResult<TestProjection>(
				[Stored ?? new TestProjection { Id = "from-inner" }],
				pageSize,
				1,
				"next"));
		}
	}
}
