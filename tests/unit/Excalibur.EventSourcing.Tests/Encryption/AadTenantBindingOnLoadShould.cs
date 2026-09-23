// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// The tenant these two decorators bind into the AES-GCM Additional Authenticated Data is the tenant the
/// operation is running as, resolved per call — not a constant stamped when the decorator was constructed.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> Both event-sourcing encryption decorators built their <see cref="EncryptionContext"/>
/// once, in their constructors, from the configured default tenant. The provider binds that value into the
/// AAD and names cross-tenant decryption as the thing it prevents — so in a multi-tenant host every record
/// carried one tenant and the advertised control separated nothing.
/// </para>
/// <para>
/// <b>WHY THE EVENT-STORE ARMS SIT ON THE LOAD PATH.</b> That decorator's write path protects personal
/// fields under the DATA SUBJECT's key, a separate scheme that never builds an
/// <see cref="EncryptionContext"/>. The envelope path is reached only on load, so the load path is where the
/// tenant it binds is observable at all. The projection decorator encrypts on its own write path and is
/// bound there.
/// </para>
/// <para>
/// <b>SCOPE.</b> That the provider HONOURS the tenant it is handed — real AES-GCM, tenant B cannot open
/// tenant A's ciphertext — is bound where the provider lives. These arms bind the other half: that the
/// caller feeds it the tenant of the operation rather than a constant.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AadTenantBindingOnLoadShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	private static readonly CancellationToken Ct = CancellationToken.None;

	/// <summary>
	/// SAFETY — the defect proper, on the event store. Two loads under two ambient tenants must hand the
	/// provider two different tenants. Identical values here ARE the original bug.
	/// </summary>
	[Fact]
	public async Task BindTheAmbientTenantPerLoadOnTheEventStore()
	{
		var tenant = new MutableTenantContext(TenantA);
		var captured = new List<string?>();

		var decorator = CreateEventStoreDecorator(captured, tenant);

		_ = await decorator.LoadAsync("agg-1", "Order", Ct);
		tenant.TenantId = TenantB;
		_ = await decorator.LoadAsync("agg-1", "Order", Ct);

		captured.Count.ShouldBe(2, "both loads must have reached the provider, or this arm proves nothing");

		captured[0].ShouldBe(TenantA);
		captured[1].ShouldBe(
			TenantB,
			"the second load ran under a different ambient tenant and must carry it. A constant here is the "
			+ "original defect: every record shared one AAD tenant, so the control separated nothing");
	}

	/// <summary>
	/// SAFETY — there is no second answer. A decorator that could be built without a tenant context would
	/// bind one tenant when a context is registered and another when it is not, so the constructor refuses
	/// the absent state instead of substituting a constant for it.
	/// </summary>
	[Fact]
	public void RequireATenantContextOnTheEventStore()
	{
		_ = Should.Throw<ArgumentNullException>(() => CreateEventStoreDecorator([], tenantContext: null!));
	}

	/// <summary>
	/// SAFETY — the same defect on the projection store, which encrypts on its own write path. A separate
	/// arm because a fix applied to one decorator leaves the other free to regress silently.
	/// </summary>
	[Fact]
	public async Task BindTheAmbientTenantPerOperationOnTheProjectionStore()
	{
		var tenant = new MutableTenantContext(TenantA);
		var captured = new List<string?>();

		var decorator = CreateProjectionDecorator(captured, tenant);

		await SaveOneAsync(decorator).ConfigureAwait(false);
		tenant.TenantId = TenantB;
		await SaveOneAsync(decorator).ConfigureAwait(false);

		captured.Count.ShouldBe(2, "both writes must have reached the provider, or this arm proves nothing");

		captured[0].ShouldBe(TenantA);
		captured[1].ShouldBe(TenantB, "the projection decorator must resolve the ambient tenant per operation");
	}

	/// <summary>SAFETY for the projection path — the absent state is refused, not defaulted.</summary>
	[Fact]
	public void RequireATenantContextOnTheProjectionStore()
	{
		_ = Should.Throw<ArgumentNullException>(() => CreateProjectionDecorator([], tenantContext: null!));
	}

	/// <summary>
	/// LIVENESS — a single-tenant host registers no tenant context of its own, and the real registration must
	/// still resolve, binding the framework's single-tenant identity. Without this arm the required parameter
	/// could have been satisfied by a registration that fails to resolve in every single-tenant host.
	/// </summary>
	[Fact]
	public async Task BindTheSingleTenantIdentityThroughTheRealRegistrationWhenNoTenantContextIsRegistered()
	{
		var captured = new List<string?>();

		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(
			services, EncryptingRegistry(captured));
		_ = Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(
			services, OptionsWithDefault());
		_ = Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(
			services, A.Fake<IProjectionStore<TestProjection>>());

		_ = Microsoft.Extensions.DependencyInjection.EventSourcingUtilitiesServiceCollectionExtensions
			.AddProjectionEncryption<TestProjection>(services);

		await using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
			.BuildServiceProvider(services);
		var store = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
			.GetRequiredService<IProjectionStore<TestProjection>>(provider);

		store.ShouldBeOfType<EncryptingProjectionStoreDecorator<TestProjection>>(
			"precondition: the registration must actually have decorated the store");

		await SaveOneAsync(store).ConfigureAwait(false);

		captured.ShouldHaveSingleItem().ShouldBe(
			TenantDefaults.DefaultTenantId,
			"a single-tenant host binds the framework's one canonical tenant, not an empty value");
	}

	private static EncryptingEventStoreDecorator CreateEventStoreDecorator(
		List<string?> captured, ITenantContext tenantContext)
	{
		var inner = A.Fake<IEventStore>();
		_ = A.CallTo(() => inner.LoadAsync("agg-1", "Order", Ct)).Returns<IReadOnlyList<StoredEvent>>(
		[
			new StoredEvent(
				"evt-1", "agg-1", "Order", "PlainOrderPlaced", Envelope(), null, 1, DateTimeOffset.UtcNow),
		]);

		return new EncryptingEventStoreDecorator(
			inner,
			DecryptingRegistry(captured),
			new SubjectFieldCryptor(A.Fake<IFieldEncryptor>()),
			A.Fake<IEventSerializer>(),
			OptionsWithDefault(),
			tenantContext);
	}

	private static EncryptingProjectionStoreDecorator<TestProjection> CreateProjectionDecorator(
		List<string?> captured, ITenantContext tenantContext) =>
		new(
			A.Fake<IProjectionStore<TestProjection>>(),
			EncryptingRegistry(captured),
			OptionsWithDefault(),
			tenantContext);

	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
		"AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Test fixture: the projection type is concrete and rooted by this assembly.")]
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
		"AOT", "IL3050:RequiresDynamicCode",
		Justification = "Test fixture: the projection type is concrete and rooted by this assembly.")]
	private static Task SaveOneAsync(IProjectionStore<TestProjection> store) =>
		store.UpsertAsync("p-1", new TestProjection { SensitiveData = [1, 2, 3] }, Ct);

	/// <summary>A registry whose DECRYPTION provider records the tenant each load hands it.</summary>
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

	/// <summary>A registry whose PRIMARY provider records the tenant each write hands it.</summary>
	private static IEncryptionProviderRegistry EncryptingRegistry(List<string?> captured)
	{
		var provider = A.Fake<IEncryptionProvider>();
		_ = A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((byte[] _, EncryptionContext ctx, CancellationToken _) => captured.Add(ctx.TenantId))
			.Returns(Task.FromResult(Encrypted()));

		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.GetPrimary()).Returns(provider);
		return registry;
	}

	private static IOptions<EncryptionOptions> OptionsWithDefault() =>
		Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
		});

	private static EncryptedData Encrypted() => new()
	{
		Ciphertext = [1],
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		KeyId = "k",
		KeyVersion = 1,
		Iv = new byte[12],
	};

	/// <summary>Framework-envelope bytes, so the decorator takes its decrypt branch rather than passing through.</summary>
	private static byte[] Envelope()
	{
		var json = JsonSerializer.SerializeToUtf8Bytes(Encrypted());
		var result = new byte[EncryptedData.MagicBytes.Length + json.Length];
		EncryptedData.MagicBytes.CopyTo(result.AsSpan());
		json.CopyTo(result, EncryptedData.MagicBytes.Length);
		return result;
	}

	/// <summary>An ambient tenant the arms can move between operations.</summary>
	private sealed class MutableTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; set; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

#pragma warning disable CA1034 // Nested types should not be visible — required for FakeItEasy proxy generation

	/// <summary>A projection with one encrypted field, so the write path reaches the provider.</summary>
	public sealed class TestProjection
	{
		public string Id { get; set; } = "p-1";

		[EncryptedField]
		public byte[]? SensitiveData { get; set; }
	}

#pragma warning restore CA1034
}
