// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;
using System.Reflection;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Channels;

/// <summary>
/// Structural-absence lock for Sprint 848 Lane A1 (<c>esx8ia</c>): the dead, never-implemented
/// <c>Excalibur.Dispatch.Channels.IMessageChannelAdapter&lt;TMessage&gt;</c> duplicate has been deleted.
/// The supported contract is the single <c>Excalibur.Dispatch.IMessageChannelAdapter&lt;TMessage&gt;</c>
/// (in <c>Excalibur.Dispatch.Abstractions</c>, implemented by <c>SqsChannelAdapter</c>).
/// </summary>
/// <remarks>
/// Reflection-based by design so the lock compiles whether or not the dead type exists: it is RED on the
/// pre-fix parent (the dead type is present) and GREEN only once the type is removed. This is the test-side
/// pair of the canonical <c>PublicAPI.Shipped.txt</c> baseline removal (RS0017), guarding against a
/// regression that re-introduces the duplicate and resurrects the CS0104 ambiguity risk (AC-A1.4).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DeadMessageChannelAdapterRemovedShould
{
	// Anchor on a stable public type that lives in the same Excalibur.Dispatch core assembly and the
	// same Excalibur.Dispatch.Channels namespace as the (former) dead interface.
	private static readonly Assembly DispatchAssembly = typeof(ChannelMode).Assembly;

	private const string DeadTypeFullName = "Excalibur.Dispatch.Channels.IMessageChannelAdapter`1";
	private const string DeadNamespace = "Excalibur.Dispatch.Channels";

	[Fact]
	public void NotExposeTheDeadChannelsVariantByFullName()
	{
		var deadType = DispatchAssembly.GetType(DeadTypeFullName, throwOnError: false);

		deadType.ShouldBeNull(
			"the dead Excalibur.Dispatch.Channels.IMessageChannelAdapter<TMessage> duplicate must be deleted (S848 A1 / esx8ia)");
	}

	[Fact]
	public void NotDeclareAnyIMessageChannelAdapterTypeInTheChannelsNamespace()
	{
		var offenders = DispatchAssembly
			.GetTypes()
			.Where(t => string.Equals(t.Namespace, DeadNamespace, System.StringComparison.Ordinal)
				&& t.Name.StartsWith("IMessageChannelAdapter", System.StringComparison.Ordinal))
			.Select(t => t.FullName)
			.ToArray();

		offenders.ShouldBeEmpty(
			"no IMessageChannelAdapter contract may live in the Excalibur.Dispatch.Channels namespace; "
			+ "the single supported contract is Excalibur.Dispatch.IMessageChannelAdapter<TMessage> in Abstractions");
	}
}
