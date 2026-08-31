// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// Regression guard for bd-x6rg45: Outbox prerequisite validator must fail loud at host
/// start when the consumer calls <c>AddOutbox(...)</c> but omits a concrete outbox store
/// provider, and must pass cleanly when a polling or change-feed provider is present.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excalibur_Dispatch-5kwz1m.</b> Before this fix, a host wired through
/// <c>AddOutbox(o => o.UseCosmosDb/UseDynamoDb/UseFirestore(...))</c> could not start: this validator
/// threw at <see cref="IHostedService.StartAsync"/> reporting that no provider was configured, naming
/// only the polling providers, even though the consumer had correctly called a change-feed provider
/// extension. The three <c>Succeed_When*StoreIsRegistered</c> arms below are the liveness proof that a
/// change-feed host now starts; they are RED against the pre-fix validator (it only ever checked keyed
/// "default" <see cref="IOutboxStore"/>). <see cref="PollingProviderStillResolvesKeyedDefaultAndAlias"/>
/// is the safety/regression arm proving a polling provider's keyed "default" store and non-keyed
/// <see cref="IOutboxStore"/> alias are unchanged by this fix.
/// </para>
/// <para>
/// Every arm below wires the host through the real production path (<c>AddExcalibur</c> to
/// <c>AddOutbox</c> to the provider's own <c>Use*</c> method) and resolves from a real
/// <see cref="ServiceProvider"/> — no infrastructure connects: each store captures its options/client and
/// validates them without connecting.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenIOutboxStoreIsMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddOutbox(...) must register OutboxPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("IOutboxStore", Case.Sensitive);
		ex.Message.ShouldContain("AddOutbox", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenKeyedIOutboxStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => A.Fake<IOutboxStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStore_WhenKeyedDefaultIsRegistered()
	{
		// The non-keyed IOutboxStore convenience alias forwards to keyed "default",
		// so consumers can inject IOutboxStore directly without [FromKeyedServices]. The alias is only
		// registered when AddOutbox(...) sees the keyed "default" store BACKED at configure time
		// (RegisterOutboxStoreAliasesIfBacked), so the keyed registration must precede AddOutbox(...).
		var fakeStore = A.Fake<IOutboxStore>();
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStore>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStore alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStoreAdmin_WhenKeyedDefaultIsRegistered()
	{
		// When a provider (e.g. Elasticsearch) registers IOutboxStoreAdmin as keyed "default",
		// the non-keyed alias should forward to it. Registration order matters: see the remark above.
		var fakeAdmin = A.Fake<IOutboxStoreAdmin>();
		var fakeStore = A.Fake<IOutboxStore>();
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);
		services.AddKeyedSingleton<IOutboxStoreAdmin>("default", (_, _) => fakeAdmin);
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStoreAdmin>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStoreAdmin alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeAdmin);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStoreAdmin_ViaCastFromIOutboxStore()
	{
		// When no separate IOutboxStoreAdmin keyed registration exists (most providers),
		// the forwarding falls back to casting from the keyed IOutboxStore. Registration order matters:
		// see the remark above.
		var fakeStore = A.Fake<IOutboxStore>(x => x.Implements<IOutboxStoreAdmin>());
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStoreAdmin>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStoreAdmin should fall back to casting from IOutboxStore.");
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public void Register_Once_EvenWhenAddOutboxCalledTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}

	[Fact]
	public async Task ExceptionMessage_MentionsChangeFeedProviders_WhenNothingIsRegistered()
	{
		// Excalibur_Dispatch-5kwz1m: the pre-fix message named only the polling providers
		// (UseSqlServer/UsePostgres/UseMongoDB), so a consumer who called UseCosmosDb read a message
		// telling them they had not called a provider extension at all. Guidance must cover both families.
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));

		ex.Message.ShouldContain("UseCosmosDb", Case.Sensitive);
		ex.Message.ShouldContain("UseDynamoDb", Case.Sensitive);
		ex.Message.ShouldContain("UseFirestore", Case.Sensitive);
		ex.Message.ShouldContain("UseSqlServer", Case.Sensitive);
	}

	#region Excalibur_Dispatch-5kwz1m: change-feed hosts start (liveness)

	[Fact]
	public async Task Succeed_WhenCosmosDbStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseCosmosDb(cosmos => cosmos
			.ConnectionString("AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==")
			.DatabaseName("outbox_unused"))));

		await using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await Should.NotThrowAsync(
			() => validator.StartAsync(CancellationToken.None),
			"A host wired through UseCosmosDb(...) must start: it registers ICloudNativeOutboxStore, the " +
			"change-feed family's satisfying contract, even though it never registers a keyed 'default' " +
			"IOutboxStore.");

		// The change-feed contract is what actually resolves; IOutboxStore is honestly absent, not a
		// dangling forwarder.
		provider.GetService<ICloudNativeOutboxStore>().ShouldNotBeNull();
		provider.GetService<IOutboxStore>().ShouldBeNull(
			"A change-feed host does not implement IOutboxStore; the non-keyed alias must not advertise one.");
	}

	[Fact]
	public async Task Succeed_WhenDynamoDbStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseDynamoDb(db =>
			db.ServiceUrl("http://localhost:8000"))));

		await using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await Should.NotThrowAsync(
			() => validator.StartAsync(CancellationToken.None),
			"A host wired through UseDynamoDb(...) must start through the same ICloudNativeOutboxStore path " +
			"as UseCosmosDb(...).");

		provider.GetService<ICloudNativeOutboxStore>().ShouldNotBeNull();
		provider.GetService<IOutboxStore>().ShouldBeNull();
	}

	[Fact]
	public async Task Succeed_WhenFirestoreStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseFirestore(fs =>
			fs.ProjectId("test-project"))));

		await using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await Should.NotThrowAsync(
			() => validator.StartAsync(CancellationToken.None),
			"A host wired through UseFirestore(...) must start through the same ICloudNativeOutboxStore " +
			"path as UseCosmosDb(...).");

		provider.GetService<ICloudNativeOutboxStore>().ShouldNotBeNull();
		provider.GetService<IOutboxStore>().ShouldBeNull();
	}

	#endregion

	[Fact]
	public async Task PollingProviderStillResolvesKeyedDefaultAndAlias()
	{
		// Safety/regression arm (Excalibur_Dispatch-5kwz1m): a polling provider's keyed "default"
		// IOutboxStore and the non-keyed convenience alias must be UNCHANGED by making the alias
		// tolerant of an absent change-feed registration.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseSqlServer(sql =>
			sql.ConnectionString("Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;"))));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));

		var keyed = provider.GetKeyedService<IOutboxStore>("default");
		keyed.ShouldNotBeNull("A polling provider must still register a keyed 'default' IOutboxStore.");

		var nonKeyed = provider.GetService<IOutboxStore>();
		nonKeyed.ShouldNotBeNull("The non-keyed IOutboxStore alias must still forward for polling providers.");
		nonKeyed.ShouldBeSameAs(keyed, "The alias must forward to the SAME instance as keyed 'default'.");
	}
}