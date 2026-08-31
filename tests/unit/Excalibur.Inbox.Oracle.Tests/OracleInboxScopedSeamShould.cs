// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// The inbox middleware selects its exactly-once path by type-testing the store for
/// <see cref="IScopedTransactionalInboxStore"/> and consulting
/// <see cref="IInboxStoreCapabilities.SupportsTransactional"/>. An undecorated Oracle store reported
/// <c>SupportsTransactional = true</c> while failing that type test, so it advertised the atomic path and
/// always fell through to the at-least-once claim protocol.
/// </summary>
/// <remarks>
/// These arms evaluate the middleware's selection predicate against a REAL constructed store rather than
/// over <c>typeof</c>, so they cannot pass against a type that is never instantiated. What they do NOT cover
/// is the atomic commit itself — that the handler's writes and the processed-mark land in one Oracle
/// transaction — which requires a live Oracle instance and is covered by the real-infrastructure
/// conformance suite, not here.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class OracleInboxScopedSeamShould
{
	// The store connects lazily, so constructing it touches no database.
	private static OracleInboxStore CreateStore() =>
		new(
			Options.Create(new OracleInboxOptions { ConnectionString = "User Id=x;Password=y;Data Source=unused" }),
			NullLogger<OracleInboxStore>.Instance,
			tenantContext: new TestTenantContext(),
			tenantContextOptions: Options.Create(new TenantContextOptions()));

	[Fact]
	public void SatisfyTheMiddlewareAtomicPathPredicate_WhenUndecorated()
	{
		IInboxStore store = CreateStore();

		// Both halves of InboxMiddleware's selection, evaluated on the real instance the middleware would hold.
		var supportsTransactional = store is IInboxStoreCapabilities capabilities
			? capabilities.SupportsTransactional
			: store is IScopedTransactionalInboxStore;

		supportsTransactional.ShouldBeTrue();
		store.ShouldBeAssignableTo<IScopedTransactionalInboxStore>(
			"an undecorated Oracle store that reports SupportsTransactional must expose the scoped seam, " +
			"or the middleware silently downgrades it to the at-least-once claim protocol.");
	}

	[Fact]
	public void KeepTheRelationalSeam_SoTheScopedSeamIsABridgeAndNotAReplacement()
	{
		IInboxStore store = CreateStore();

		// LIVENESS ARM: the scoped seam is implemented by delegating to the relational overload. Deleting the
		// relational seam to "satisfy" the arm above would pass it and destroy the implementation beneath it.
		store.ShouldBeAssignableTo<ITransactionalInboxStore>();
		((IInboxStoreCapabilities)store).SupportsClaim.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectANullHandler_OnTheScopedOverload()
	{
		var store = (IScopedTransactionalInboxStore)CreateStore();

		// Awaited inside ThrowAsync so the arm holds whether the guard throws eagerly (it does today,
		// before the ValueTask is returned) or from inside the async path -- it asserts the same thing
		// either way, and does not depend on which one the implementation happens to do.
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.TryProcessTransactionallyAsync("msg-1", "handler-1", null!, CancellationToken.None));
	}
}
