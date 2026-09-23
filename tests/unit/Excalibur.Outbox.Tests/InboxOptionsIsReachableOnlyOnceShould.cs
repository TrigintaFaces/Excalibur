// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// A consumer must be able to reach exactly one public <c>InboxOptions</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Outbox package shipped a second public type of that name: immutable, with an internal constructor,
/// an internal builder interface and internal factory methods, so nothing outside the assembly could
/// construct or configure it — and nothing inside read it either. Both the inbox processor and the message
/// inbox bind the delivery options in the Dispatch package, which is the surface a consumer can actually
/// set. The Outbox copy was an unreachable duplicate sitting in the package named for the other half of the
/// pattern, and its only effect on a consumer was to make naming the live type from a file that imported
/// both namespaces a compile error.
/// </para>
/// <para>
/// This arm binds the property rather than the deletion: it asks the assembly what a consumer can see, so
/// it goes red if the type comes back under any name in this namespace, including as a reintroduced
/// configuration shape someone thought was new.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxOptionsIsReachableOnlyOnceShould
{
	private static readonly Assembly OutboxAssembly = typeof(OutboxOptions).Assembly;

	[Fact]
	public void ExposeNoPublicInboxOptionsFromTheOutboxPackage()
	{
		// SAFETY. A second public InboxOptions here is reachable by a consumer who imports both namespaces,
		// and the two cannot then be told apart by name.
		var offenders = OutboxAssembly.GetExportedTypes()
			.Where(static t => t.Name is "InboxOptions" or "InboxBatchOptions" or "InboxPreset")
			.Select(static t => t.FullName)
			.ToList();

		offenders.ShouldBeEmpty(
			"The Outbox package must not export an inbox configuration type. Inbox processing options are "
			+ "owned by the delivery namespace in the Dispatch package, which is what the inbox processor "
			+ "and the message inbox actually bind; a second public copy here cannot be configured by a "
			+ "consumer and collides with the live one by name.");
	}

	[Fact]
	public void StillExposeTheOutboxConfigurationSurfaceItDoesOwn()
	{
		// LIVENESS + positive control for the arm above. Without this, the safety arm is satisfied by an
		// assembly that exports nothing at all — or by a reflection query that silently matches nothing —
		// and the deletion could have taken the package's real configuration surface with it.
		var exported = OutboxAssembly.GetExportedTypes().Select(static t => t.Name).ToList();

		exported.ShouldContain(
			nameof(OutboxOptions),
			"The Outbox package must still export the outbox options it does own. If this is missing the "
			+ "query is broken or the wrong surface was removed, and the safety arm above proves nothing.");
	}

	[Fact]
	public void KeepTheDeliveryInboxOptionsPublicAndSettable()
	{
		// LIVENESS. The type that survived has to be the one a consumer can configure — an immutable
		// survivor would satisfy "exactly one" while leaving the inbox unconfigurable.
		var surviving = typeof(Excalibur.Dispatch.Options.Delivery.InboxOptions);

		surviving.IsPublic.ShouldBeTrue("the surviving inbox options must be reachable by a consumer.");
		surviving.GetConstructor(Type.EmptyTypes).ShouldNotBeNull(
			"the surviving inbox options must be constructible so IOptions<T> can materialise it.");
		surviving.GetProperty(nameof(Excalibur.Dispatch.Options.Delivery.InboxOptions.MaxAttempts))!
			.CanWrite.ShouldBeTrue(
				"the surviving inbox options must be settable — a consumer configures the inbox through it.");
	}
}
