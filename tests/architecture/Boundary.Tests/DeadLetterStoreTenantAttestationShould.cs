// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Boundary.Tests;

/// <summary>
/// Binds the dead-letter store's tenant-scoping guarantee to a mechanism, so the guarantee its own
/// documentation states is kept rather than merely written.
/// </summary>
/// <remarks>
/// <para>
/// A dead-letter entry carries the failed message body, so an estate-wide read hands one tenant another
/// tenant's message content. The contract said so in prose for weeks while carrying no marker the
/// multi-tenancy floor could see, which meant a consumer-supplied store that ignored the ambient tenant
/// was accepted in silence. Prose is not a control.
/// </para>
/// <para>
/// <b>The attestation cannot be true while the property is false, and that is the point of these arms.</b>
/// The capability marker is emitted only by the registration seam that resolves and supplies the store its
/// <c>ITenantContext</c>, in the same act; the marker interface's only member is internal to the
/// abstractions assembly, so no provider outside that assembly can register a look-alike beside a store
/// built without the ambient tenant. This repository has shipped the opposite arrangement before - a
/// separately-registered marker that read as truthful on an unwired store - so the last arm here asserts
/// the structural lock itself, which is otherwise the kind of property that dies silently.
/// </para>
/// <para>
/// These compose a real container through the production registration path rather than asserting on
/// descriptors, because "a marker descriptor is present" and "the host actually starts" are different
/// claims, and only the second is what a consumer experiences.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class DeadLetterStoreTenantAttestationShould
{
	/// <summary>
	/// A consumer's own store that never reads an ambient tenant - the shape the floor exists to refuse.
	/// </summary>
	private sealed class UnscopedConsumerDeadLetterStore : IDeadLetterStore
	{
		public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken) =>
			Task.FromResult<DeadLetterMessage?>(null);

		public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
			DeadLetterFilter filter,
			CancellationToken cancellationToken) =>
			Task.FromResult<IEnumerable<DeadLetterMessage>>([]);

		public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken) =>
			Task.FromResult(false);
	}

	private static void ComposeRowDiscriminator(IServiceCollection services) =>
		services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

	// ---------- SAFETY: an unattested consumer store is refused ----------

	[Fact]
	public void Refuse_a_consumer_store_that_presents_no_tenancy_capability()
	{
		// Measured, rather than asserted: this arm is carried by the composition-time gate block, and it
		// stays green with the contract's tenant-owned marker removed. Said plainly because the obvious
		// reading is wrong - the marker's own load-bearing arm is the after-composition one below, which
		// does go red without it. Two mechanisms cover two different windows, and only naming which is
		// which keeps a future reader from deleting one on the evidence of the other.
		var services = new ServiceCollection();
		services.AddLogging();
		services.TryAddSingleton<IDeadLetterStore, UnscopedConsumerDeadLetterStore>();

		var thrown = Should.Throw<InvalidOperationException>(() => ComposeRowDiscriminator(services));

		thrown.Message.ShouldContain(nameof(IDeadLetterStore));
	}

	[Fact]
	public void Refuse_a_consumer_store_registered_AFTER_multi_tenancy_was_composed()
	{
		// This is the arm the contract's own marker carries, and nothing else does. The composition-time
		// gate reads the registrations present at the instant it runs, so a store added afterwards is
		// invisible to it - and adding a store after composing multi-tenancy is a supported ordering. The
		// startup re-assertion closes that window by sweeping the completed collection for every contract
		// DECLARING itself tenant-owned, so a contract that does not declare it is simply never asked.
		// Remove the declaration from the contract and this arm goes green while the hole is open.
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPoisonMessageHandling();
		ComposeRowDiscriminator(services);

		// After the fact, and unattested: the shape a consumer produces by registering their own store
		// below their multi-tenancy call.
		_ = services.AddSingleton<IDeadLetterStore, UnscopedConsumerDeadLetterStore>();
		_ = services.RemoveAll<ITenantScopingCapability<IDeadLetterStore>>();

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IStartupPrerequisiteValidator>().ToList();

		// Non-vacuity: an empty validator set makes the loop below throw nothing and the arm pass for the
		// wrong reason - the classic shape of a gate satisfied by never running.
		validators.ShouldNotBeEmpty();

		var thrown = Should.Throw<InvalidOperationException>(() =>
		{
			foreach (var validator in validators)
			{
				validator.Validate();
			}
		});

		thrown.Message.ShouldContain(nameof(IDeadLetterStore));
	}

	// ---------- LIVENESS: every shipped registration path composes ----------

	[Fact]
	public void Accept_the_default_store_registered_by_poison_message_handling()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPoisonMessageHandling();

		Should.NotThrow(() => ComposeRowDiscriminator(services));

		services.ShouldContain(d => d.ServiceType == typeof(ITenantScopingCapability<IDeadLetterStore>));
	}

	[Fact]
	public void Accept_the_store_registered_by_the_explicit_in_memory_override()
	{
		// Red before this override was routed through the seam. It hand-rolled the construction, supplying
		// the tenant context correctly but emitting no attestation - so once the contract became
		// tenant-owned, a host that called it was refused for a store that does scope. The repair had to be
		// the seam and not a marker registered alongside, or the fix would have re-created the very leak the
		// marker exists to make impossible.
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddInMemoryDeadLetterStore();

		Should.NotThrow(() => ComposeRowDiscriminator(services));

		services.ShouldContain(d => d.ServiceType == typeof(ITenantScopingCapability<IDeadLetterStore>));
	}

	[Fact]
	public void Hand_the_in_memory_override_a_fresh_store_on_every_call()
	{
		// The override's contract is REPLACE-and-reset, and routing it through a first-wins seam would have
		// silently kept the first call's accumulated entries. Locked here because the seam change is what
		// put that at risk.
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddInMemoryDeadLetterStore();
		using var first = services.BuildServiceProvider();
		var firstStore = first.GetRequiredService<IDeadLetterStore>();

		_ = services.AddInMemoryDeadLetterStore();
		using var second = services.BuildServiceProvider();
		var secondStore = second.GetRequiredService<IDeadLetterStore>();

		secondStore.ShouldNotBeSameAs(firstStore);
	}

	// ---------- The attestation is structurally inseparable from the wiring ----------

	[Fact]
	public void Keep_the_capability_marker_unimplementable_outside_the_abstractions_assembly()
	{
		// The lock that makes every arm above mean what it says. If this member is ever widened to public,
		// a provider can register a truthful-looking marker beside a store built with no tenant context,
		// and the floor's answer becomes an assertion by the provider rather than a fact about the wiring.
		var members = typeof(ITenantScopingCapability<IDeadLetterStore>).GetMethods(
			System.Reflection.BindingFlags.Instance
			| System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.NonPublic);

		members.ShouldNotBeEmpty();
		members.ShouldAllBe(m => !m.IsPublic);
	}
}
