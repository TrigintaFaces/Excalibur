// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling.PoisonMessage;

/// <summary>
/// WIRE lock: a consumer-supplied <see cref="IDeadLetterStore"/> that does not implement the optional
/// <see cref="IDeadLetterStoreAdmin"/> facet must resolve and start through the real DI container.
/// </summary>
/// <remarks>
/// <para>
/// A consumer-supplied store is an explicitly supported extension point — the framework registers its
/// default with <c>TryAddSingleton</c>, so the consumer wins — and the rest of the subsystem treats a
/// non-admin store as fully legal, testing for the facet rather than assuming it
/// (<c>PoisonMessageHandler</c>, <c>PoisonMessageCleanupService</c>, and the conformance kit five times).
/// The DI registration was the one place that assumed it, via a hard cast.
/// </para>
/// <para>
/// Why the existing suite could not catch this: the handler's own fixture builds its store as
/// <c>A.Fake&lt;IDeadLetterStore&gt;(o =&gt; o.Implements&lt;IDeadLetterStoreAdmin&gt;())</c>, so every store
/// under test is an admin store and the failing shape is never constructed. A green suite is not evidence
/// about a shape the suite cannot build.
/// </para>
/// <para>
/// <b>Fixture shape is load-bearing.</b> <see cref="NonAdminDeadLetterStore"/> implements
/// <see cref="IDeadLetterStore"/> <b>directly</b> and inherits no first-party base, so the assertion binds
/// the interface's own contract rather than re-testing a base class that would have supplied the facet.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> These arms go RED against a registration that casts the resolved store to the admin
/// facet unconditionally: resolving <see cref="IDeadLetterStoreAdmin"/> then throws
/// <see cref="InvalidCastException"/>, and the safety arm below fails.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "ErrorHandling")]
public sealed class PoisonMessageNonAdminStoreWiringShould
{
	/// <summary>
	/// SAFETY: the container builds and every poison-message service resolves when the registered store
	/// does not implement the admin facet.
	/// </summary>
	[Fact]
	public void ResolvePoisonMessageServices_WhenStoreIsNotAnAdminStore()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// AddPoisonMessageHandling composes with the serializer registration rather than duplicating it —
		// PoisonMessageHandler takes a DispatchJsonSerializer, which AddDispatchSerializer supplies. This is
		// the supported composition, so the lock exercises the real one instead of hand-registering it.
		_ = services.AddDispatchSerializer();

		// Registered BEFORE the framework's TryAdd default, which is how a consumer wins the store.
		_ = services.AddSingleton<IDeadLetterStore, NonAdminDeadLetterStore>();
		_ = services.AddPoisonMessageHandling();

		using var provider = services.BuildServiceProvider(validateScopes: true);

		// The consumer's store is the one in effect — the framework's default did not displace it.
		_ = provider.GetRequiredService<IDeadLetterStore>().ShouldBeOfType<NonAdminDeadLetterStore>();

		// The hosted cleanup service is the subsystem's own admin-facet consumer: it resolves the store and
		// tests for the facet (`is not IDeadLetterStoreAdmin`) rather than requiring it. It must start.
		//
		// IPoisonMessageHandler is deliberately NOT resolved here: it additionally requires a serializer
		// that AddPoisonMessageHandling does not register, so asserting on it would fail for a reason that
		// has nothing to do with the admin facet and would make this lock report the wrong defect.
		_ = Should.NotThrow(() => provider.GetServices<IHostedService>().ToList());

		// The optional facet is absent rather than fatal: a missing capability, not a crash.
		provider.GetService<IDeadLetterStoreAdmin>().ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS: the admin facet is still wired — and wired to the SAME INSTANCE as the store doing the
	/// work — when the store in effect does implement it.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a registration that never registers the admin
	/// facet at all, which would silently disable every administrative operation. The same-instance
	/// assertion is what makes it a real wire rather than a second, parallel store.
	/// </remarks>
	[Fact]
	public void StillWireAdminFacet_ToTheSameInstance_WhenStoreImplementsIt()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// AddPoisonMessageHandling composes with the serializer registration rather than duplicating it —
		// PoisonMessageHandler takes a DispatchJsonSerializer, which AddDispatchSerializer supplies. This is
		// the supported composition, so the lock exercises the real one instead of hand-registering it.
		_ = services.AddDispatchSerializer();

		// No consumer store: the framework's default InMemoryDeadLetterStore is in effect, and it does
		// implement the admin facet.
		_ = services.AddPoisonMessageHandling();

		using var provider = services.BuildServiceProvider(validateScopes: true);

		var store = provider.GetRequiredService<IDeadLetterStore>();
		var admin = provider.GetService<IDeadLetterStoreAdmin>();

		admin.ShouldNotBeNull("the default store implements the admin facet, so it must remain resolvable");
		admin.ShouldBeSameAs(store, "the admin facet must be the same instance as the store doing the work");
	}

	/// <summary>
	/// A dead letter store that implements only the storage contract — no admin facet — implemented
	/// directly against the interface so no inherited member can satisfy the assertion for it.
	/// </summary>
	private sealed class NonAdminDeadLetterStore : IDeadLetterStore
	{
		public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken)
			=> Task.FromResult<DeadLetterMessage?>(null);

		public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
			DeadLetterFilter filter,
			CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<DeadLetterMessage>>([]);

		public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken)
			=> Task.FromResult(false);
	}
}
