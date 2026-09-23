// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// The tenant bound into the AES-GCM Additional Authenticated Data is the tenant of the DATA, resolved per
/// operation — not a process-wide constant stamped when the decorator was constructed.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> Five store-encryption paths built an <see cref="EncryptionContext"/> ONCE, in their
/// constructors, with the tenant taken from a configured constant. The provider binds that value into
/// the AAD and its own comment names cross-tenant decryption as the thing it prevents — so in a
/// multi-tenant host every record carried the SAME tenant and the advertised cross-tenant control
/// contributed nothing, on exactly the paths that encrypt stored data. Neither the key nor the AAD varied
/// by tenant: a falsely advertised control, not a weakened one.
/// </para>
/// <para>
/// <b>TWO ARMS FOR TWO HALVES, because neither proves the composition alone.</b> The control and the
/// caller failed independently, so they are bound independently:
/// </para>
/// <list type="number">
/// <item>
/// <b>The control works</b> — against a REAL provider and real keys, ciphertext written under one tenant
/// fails its tag check under another. A faked provider cannot show this; only real AES-GCM can.
/// </item>
/// <item>
/// <b>The caller now feeds it the right value</b> — the decorator's context carries the tenant OF THE RECORD
/// and CHANGES when that tenant changes. That is the exact property that was false: the value was constant.
/// Where a record carries its own tenant (outbox message, inbox entry) that tenant is bound on both write and
/// read, because both stores are drained for every tenant from outside any tenant scope; binding the reader's
/// ambient tenant there made every encrypted record undecryptable at drain time.
/// Asserting the value crossing the seam is not a mechanism assertion here — "bind the data's tenant" IS
/// the decorator's contract, and arm 1 establishes that the provider honours what it is handed.
/// </item>
/// </list>
/// <para>
/// <b>SCOPE, so this is not read as more than it is.</b> Row-level tenant predicates remain the control
/// that stops one tenant reaching another's ciphertext at all. This binds a defence-in-depth layer that
/// was advertised and absent.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AadTenantBindingShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	private static AesGcmEncryptionProvider CreateRealProvider() =>
		new(
			new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance),
			NullLogger<AesGcmEncryptionProvider>.Instance);

	private static EncryptionContext For(string? tenantId) =>
		new() { Purpose = "test", TenantId = tenantId };

	/// <summary>
	/// SAFETY — the control itself. Real AES-GCM, real keys: tenant B cannot open tenant A's ciphertext.
	/// </summary>
	[Fact]
	public async Task RefuseToDecryptOneTenantCiphertextUnderAnotherTenant()
	{
		var provider = CreateRealProvider();
		var plaintext = "personal data"u8.ToArray();

		var encrypted = await provider.EncryptAsync(plaintext, For(TenantA), CancellationToken.None)
			.ConfigureAwait(false);

		_ = await Should.ThrowAsync<Exception>(
			async () => await provider.DecryptAsync(encrypted, For(TenantB), CancellationToken.None)
				.ConfigureAwait(false));
	}

	/// <summary>
	/// LIVENESS — and without it the arm above is satisfied by a provider that decrypts NOTHING, which is
	/// the cheapest way to pass a cross-tenant safety assertion.
	/// </summary>
	[Fact]
	public async Task StillDecryptATenantOwnCiphertext()
	{
		var provider = CreateRealProvider();
		var plaintext = "personal data"u8.ToArray();

		var encrypted = await provider.EncryptAsync(plaintext, For(TenantA), CancellationToken.None)
			.ConfigureAwait(false);

		var roundTripped = await provider.DecryptAsync(encrypted, For(TenantA), CancellationToken.None)
			.ConfigureAwait(false);

		roundTripped.ShouldBe(plaintext);
	}

	/// <summary>
	/// SAFETY — the defect proper. The decorator must hand the provider the tenant the operation is running
	/// as, and a DIFFERENT one when the ambient tenant differs. Constant here was the whole bug.
	/// </summary>
	[Fact]
	public async Task BindTheAmbientTenantPerOperation_NotAConstantFixedAtConstruction()
	{
		var tenant = new MutableTenantContext(TenantA);
		var captured = new List<string?>();

		var sut = new EncryptingInboxStoreDecorator(
			A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>()),
			CapturingRegistry(captured),
			EncryptingOptions(),
			tenant);

		await WriteOneAsync(sut).ConfigureAwait(false);
		tenant.TenantId = TenantB;
		await WriteOneAsync(sut).ConfigureAwait(false);

		captured.Count.ShouldBe(2, "both writes must have reached the provider, or this arm proves nothing");

		captured[0].ShouldBe(TenantA);
		captured[1].ShouldBe(
			TenantB,
			"the second write ran under a different ambient tenant and must carry it. Identical values here "
			+ "are the original defect: a constant stamped at construction, so every record in a "
			+ "multi-tenant host shared one AAD tenant and the control separated nothing");
	}

	/// <summary>
	/// SAFETY — there is no second answer. A decorator that could be built without a tenant context would
	/// bind one tenant when a context is registered and another when it is not, so the constructor refuses
	/// the absent state instead of substituting a configured constant for it.
	/// </summary>
	[Fact]
	public void RequireATenantContextOnTheInboxPath()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EncryptingInboxStoreDecorator(
			A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>()),
			CapturingRegistry([]),
			EncryptingOptions(),
			tenantContext: null!));
	}

	/// <summary>
	/// LIVENESS — a single-tenant host registers no tenant context of its own, and the real registration
	/// must still resolve, binding the framework's single-tenant identity rather than failing to start.
	/// </summary>
	[Fact]
	public async Task BindTheSingleTenantIdentityThroughTheRealInboxRegistrationWhenNoTenantContextIsRegistered()
	{
		var captured = new List<string?>();

		var services = new ServiceCollection();
		_ = services.AddSingleton(CapturingRegistry(captured));
		_ = services.AddSingleton(EncryptingOptions());
		_ = services.AddKeyedSingleton<IInboxStore>(
			"default", (_, _) => A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>()));

		_ = services.AddInboxEncryption();

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IInboxStore>("default");

		store.ShouldBeOfType<EncryptingInboxStoreDecorator>(
			"precondition: the registration must actually have decorated the store, or this arm measures nothing");

		await WriteOneAsync(store).ConfigureAwait(false);

		captured.ShouldHaveSingleItem().ShouldBe(
			TenantDefaults.DefaultTenantId,
			"a single-tenant host binds the framework's one canonical tenant, not an empty value");
	}

	/// <summary>
	/// SAFETY — the same defect, a DIFFERENT decorator. The outbox record carries its own tenant, so the AAD
	/// binds THAT — not the ambient tenant of whoever happens to be running. Two messages for two tenants,
	/// staged under one unrelated ambient, must carry two different AAD tenants.
	/// </summary>
	[Fact]
	public async Task BindTheMessageOwnTenantOnTheOutboxPath_NotTheAmbientOne()
	{
		var captured = new List<string?>();

		var sut = new EncryptingOutboxStoreDecorator(
			A.Fake<IOutboxStore>(), CapturingRegistry(captured), EncryptingOptions());

		await StageOneAsync(sut, TenantA).ConfigureAwait(false);
		await StageOneAsync(sut, TenantB).ConfigureAwait(false);

		captured.ShouldBe(
			[TenantA, TenantB],
			"each message binds its OWN tenant. A constant here is the original defect; an ambient tenant here "
			+ "is the defect that replaced it — the processor drains every tenant's messages from outside any "
			+ "tenant scope, so an ambient-bound AAD cannot be re-derived at drain time");
	}

	/// <summary>
	/// LIVENESS — an untenanted message. The store contract drains a message staged with no tenant as the
	/// reserved untenanted sentinel, so the AAD must bind the same value on both sides of that fold, or every
	/// untenanted encrypted message becomes undeliverable.
	/// </summary>
	[Fact]
	public async Task DeliverAnUntenantedEncryptedMessageThatTheStoreDrainsAsTheSentinel()
	{
		var (sut, staged) = RealOutbox();

		await StageOneAsync(sut, tenantId: null).ConfigureAwait(false);
		staged.ShouldHaveSingleItem().TenantId = TenantScope.UntenantedSentinel; // what every conformant store drains

		var drained = await sut.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		drained.ShouldHaveSingleItem().Payload.ShouldBe(Plaintext);
	}

	/// <summary>
	/// LIVENESS — THE DRAIN. The outbox processor fetches unsent messages for EVERY tenant before it opens
	/// any per-message tenant scope, so the ambient tenant at drain time is not the tenant that wrote the
	/// message. A message written under tenant A must still decrypt there, or no encrypted message in a
	/// multi-tenant host is ever delivered.
	/// </summary>
	[Fact]
	public async Task DeliverATenantMessageDrainedFromOutsideThatTenantsScope()
	{
		var (sut, _) = RealOutbox();

		// Staged in tenant A's request; drained by the processor, which runs outside any tenant scope. The
		// decorator has no ambient input at all, so nothing but the message's own tenant can reach the AAD.
		await StageOneAsync(sut, TenantA).ConfigureAwait(false);

		var drained = await sut.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		drained.ShouldHaveSingleItem().Payload.ShouldBe(
			Plaintext,
			"the drain runs outside the writer's tenant scope; binding the ambient tenant makes every "
			+ "encrypted message undeliverable in a multi-tenant host");
	}

	/// <summary>
	/// SAFETY — THE TRANSPLANT. Tenant A's ciphertext placed in a row that says tenant B fails its tag check.
	/// This is what the AAD binding buys once the record's own tenant is bound: a ciphertext cannot be moved
	/// between tenants' rows and still open.
	/// </summary>
	[Fact]
	public async Task RefuseToDecryptOneTenantMessageCiphertextInAnotherTenantsRow()
	{
		var (sut, staged) = RealOutbox();

		await StageOneAsync(sut, TenantA).ConfigureAwait(false);
		staged.ShouldHaveSingleItem().TenantId = TenantB;

		_ = await Should.ThrowAsync<EncryptionException>(
			async () => await sut.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false));
	}

	/// <summary>
	/// LIVENESS — the inbox retry sweep reads failed entries for every tenant from outside any tenant scope,
	/// exactly as the outbox drain does. An entry written under tenant A must still decrypt there.
	/// </summary>
	[Fact]
	public async Task DecryptATenantInboxEntryInTheAllTenantsSweep()
	{
		var ambient = new MutableTenantContext(TenantA);
		var (sut, entries) = RealInbox(ambient);

		await WriteOneAsync(sut).ConfigureAwait(false);
		ambient.TenantId = TenantScope.UntenantedSentinel; // the retry processor, outside any tenant scope

		var swept = await sut.GetAllTenantsFailedEntriesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false);

		entries.ShouldHaveSingleItem().TenantId.ShouldBe(TenantA, "precondition: the store stamped the writer's tenant");
		swept.ShouldHaveSingleItem().Payload.ShouldBe(Plaintext);
	}

	/// <summary>
	/// LIVENESS — a tenant-scoped read by the tenant that wrote the entry decrypts it.
	/// </summary>
	[Fact]
	public async Task DecryptATenantInboxEntryOnThatTenantsScopedRead()
	{
		var (sut, _) = RealInbox(new MutableTenantContext(TenantA));

		await WriteOneAsync(sut).ConfigureAwait(false);

		var entry = await sut.GetEntryAsync("any", "HandlerType", CancellationToken.None).ConfigureAwait(false);

		entry.ShouldNotBeNull().Payload.ShouldBe(Plaintext);
	}

	/// <summary>
	/// SAFETY — a tenant-scoped read binds the READER's tenant, not the tenant the row claims. If a store
	/// defect hands tenant A's entry to tenant B's scoped read, the entry must not decrypt for B, even though
	/// the row itself still says A.
	/// </summary>
	[Fact]
	public async Task RefuseToDecryptOneTenantInboxEntryOnAnotherTenantsScopedRead()
	{
		var ambient = new MutableTenantContext(TenantA);
		var (sut, entries) = RealInbox(ambient);

		await WriteOneAsync(sut).ConfigureAwait(false);
		ambient.TenantId = TenantB;

		entries.ShouldHaveSingleItem().TenantId.ShouldBe(TenantA, "precondition: the row still names the writer");

		_ = await Should.ThrowAsync<EncryptionException>(
			async () => await sut.GetEntryAsync("any", "HandlerType", CancellationToken.None).ConfigureAwait(false));
	}

	/// <summary>SAFETY — tenant A's inbox ciphertext in a row that says tenant B does not open.</summary>
	[Fact]
	public async Task RefuseToDecryptOneTenantInboxCiphertextInAnotherTenantsRow()
	{
		var (sut, entries) = RealInbox(new MutableTenantContext(TenantA));

		await WriteOneAsync(sut).ConfigureAwait(false);
		entries.ShouldHaveSingleItem().TenantId = TenantB;

		_ = await Should.ThrowAsync<EncryptionException>(
			async () => await sut.GetAllTenantsFailedEntriesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false));
	}

	/// <summary>
	/// SAFETY — THE WIRING, which every arm above is blind to. They construct the decorator by hand and pass
	/// the tenant context themselves, so all of them stay green while the real registration omits it.
	/// </summary>
	/// <remarks>
	/// That omission was the live defect: the constructor parameter is optional, so a registration that never
	/// supplies it compiles, resolves, and silently falls back to the configured constant — the fix present in
	/// the type and absent from the product. This arm resolves through the real <c>AddInboxEncryption</c> path
	/// so the argument cannot be dropped without a RED.
	/// </remarks>
	[Fact]
	public async Task ResolveTheAmbientTenantThroughTheRealInboxEncryptionRegistration()
	{
		var tenant = new MutableTenantContext(TenantA);
		var captured = new List<string?>();

		var services = new ServiceCollection();
		_ = services.AddSingleton<ITenantContext>(tenant);
		_ = services.AddSingleton(CapturingRegistry(captured));
		_ = services.AddSingleton(EncryptingOptions());
		_ = services.AddKeyedSingleton<IInboxStore>(
			"default", (_, _) => A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>()));

		_ = services.AddInboxEncryption();

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IInboxStore>("default");

		store.ShouldBeOfType<EncryptingInboxStoreDecorator>(
			"precondition: the registration must actually have decorated the store, or this arm measures nothing");

		await WriteOneAsync(store).ConfigureAwait(false);
		tenant.TenantId = TenantB;
		await WriteOneAsync(store).ConfigureAwait(false);

		captured.ShouldBe(
			[TenantA, TenantB],
			"the registration must hand the decorator the ambient tenant context. Omitting the argument leaves "
			+ "the optional parameter null, and every record in a multi-tenant host then shares the configured "
			+ "constant — the fix correct in the type and inert in the product");
	}

	/// <summary>
	/// SAFETY — the fifth path. The bulk decryption service builds the same context, so a fix applied to
	/// the four store decorators leaves it free to keep stamping the constant.
	/// </summary>
	[Fact]
	public async Task BindTheAmbientTenantPerOperationInTheBulkDecryptionService()
	{
		var tenant = new MutableTenantContext(TenantA);
		var captured = new List<string?>();

		var sut = new EncryptionDecryptionService(
			DecryptingRegistry(captured),
			EncryptingOptions(),
			tenant,
			NullLogger<EncryptionDecryptionService>.Instance);

		_ = await sut.DecryptEntityAsync(NewEntity(), new DecryptionOptions(), CancellationToken.None)
			.ConfigureAwait(false);
		tenant.TenantId = TenantB;
		_ = await sut.DecryptEntityAsync(NewEntity(), new DecryptionOptions(), CancellationToken.None)
			.ConfigureAwait(false);

		captured.Count.ShouldBe(2, "both decryptions must have reached the provider, or this arm proves nothing");

		captured[0].ShouldBe(TenantA);
		captured[1].ShouldBe(TenantB, "the bulk service must resolve the ambient tenant per operation");
	}

	/// <summary>SAFETY for the bulk service — the absent state is refused, not defaulted.</summary>
	[Fact]
	public void RequireATenantContextInTheBulkDecryptionService()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionDecryptionService(
			DecryptingRegistry([]),
			EncryptingOptions(),
			tenantContext: null!,
			NullLogger<EncryptionDecryptionService>.Instance));
	}

	private static EncryptedEntity NewEntity() => new() { Secret = Envelope() };

	/// <summary>A registry whose DECRYPTION provider records the tenant each operation hands it.</summary>
	private static IEncryptionProviderRegistry DecryptingRegistry(List<string?> captured)
	{
		var provider = A.Fake<IEncryptionProvider>();
		_ = A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((EncryptedData _, EncryptionContext ctx, CancellationToken _) => captured.Add(ctx.TenantId))
			.Returns(Task.FromResult("plaintext"u8.ToArray()));

		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);
		return registry;
	}

	/// <summary>Framework-envelope bytes, so the service takes its decrypt branch rather than passing through.</summary>
	private static byte[] Envelope()
	{
		var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new EncryptedData
		{
			Ciphertext = [1],
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyId = "k",
			KeyVersion = 1,
			Iv = new byte[12],
		});
		var result = new byte[EncryptedData.MagicBytes.Length + json.Length];
		EncryptedData.MagicBytes.CopyTo(result.AsSpan());
		json.CopyTo(result, EncryptedData.MagicBytes.Length);
		return result;
	}

#pragma warning disable CA1034 // Nested types should not be visible — required for reflective property binding

	/// <summary>An entity with one encrypted field, so the decrypt path reaches the provider.</summary>
	public sealed class EncryptedEntity
	{
		[EncryptedField]
		public byte[]? Secret { get; set; }
	}

#pragma warning restore CA1034

	/// <summary>A registry whose primary provider records the tenant each operation hands it.</summary>
	private static IEncryptionProviderRegistry CapturingRegistry(List<string?> captured)
	{
		var provider = A.Fake<IEncryptionProvider>();
		_ = A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((byte[] _, EncryptionContext ctx, CancellationToken _) => captured.Add(ctx.TenantId))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1],
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				KeyId = "k",
				KeyVersion = 1,
				Iv = new byte[12],
			}));

		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.GetPrimary()).Returns(provider);
		return registry;
	}

	private static readonly byte[] Plaintext = "personal data"u8.ToArray();

	/// <summary>A registry whose primary AND decryption provider is one real AES-GCM provider.</summary>
	private static IEncryptionProviderRegistry RealRegistry()
	{
		var provider = CreateRealProvider();
		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.GetPrimary()).Returns(provider);
		_ = A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);
		return registry;
	}

	/// <summary>An encrypting outbox over a fake store that keeps what was staged and drains it back.</summary>
	private static (EncryptingOutboxStoreDecorator Sut, List<OutboundMessage> Staged) RealOutbox()
	{
		var staged = new List<OutboundMessage>();
		var inner = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => inner.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged.Add(m))
			.Returns(ValueTask.CompletedTask);
		_ = A.CallTo(() => inner.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(staged.ToList()));

		return (new EncryptingOutboxStoreDecorator(inner, RealRegistry(), EncryptingOptions()), staged);
	}

	/// <summary>An encrypting inbox over a fake store that stamps the ambient tenant, as every store does.</summary>
	private static (EncryptingInboxStoreDecorator Sut, List<InboxEntry> Entries) RealInbox(ITenantContext ambient)
	{
		var entries = new List<InboxEntry>();
		var inner = A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>());
		_ = A.CallTo(() => inner.CreateEntryAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handler, string type, byte[] payload, IDictionary<string, object> _, CancellationToken _) =>
			{
				var entry = new InboxEntry { MessageId = id, HandlerType = handler, MessageType = type, Payload = payload, TenantId = ambient.TenantId };
				entries.Add(entry);
				return new ValueTask<InboxEntry>(entry);
			});
		_ = A.CallTo(() => inner.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<InboxEntry?>(entries.Single()));
		_ = A.CallTo(() => ((IInboxStoreAdmin)inner).GetAllTenantsFailedEntriesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>(entries.ToList()));

		return (new EncryptingInboxStoreDecorator(inner, RealRegistry(), EncryptingOptions(), ambient), entries);
	}

	private static IOptions<EncryptionOptions> EncryptingOptions() =>
		Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
		});

	private static ValueTask StageOneAsync(IOutboxStore store, string? tenantId) =>
		store.StageMessageAsync(
			new OutboundMessage
			{
				Id = $"m-{Guid.NewGuid():N}",
				MessageType = "MessageType",
				Payload = Plaintext.ToArray(),
				CreatedAt = DateTimeOffset.UtcNow,
				TenantId = tenantId,
			},
			CancellationToken.None);

	private static Task WriteOneAsync(IInboxStore store) =>
		store.CreateEntryAsync(
			$"msg-{Guid.NewGuid():N}",
			"HandlerType",
			"MessageType",
			Plaintext.ToArray(),
			new Dictionary<string, object>(StringComparer.Ordinal),
			CancellationToken.None).AsTask();

	/// <summary>An ambient tenant the arm can move between operations.</summary>
	private sealed class MutableTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; set; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
