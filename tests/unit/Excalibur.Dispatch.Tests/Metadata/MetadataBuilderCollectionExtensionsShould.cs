// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Metadata;

/// <summary>
/// Author≠impl regression lock for bead <c>yhoc4c</c> (sprint 855, P1 — silent data loss on a public
/// builder API): the <see cref="MetadataBuilderCollectionExtensions"/> <c>Add*</c> methods (reached when
/// the builder is typed as <see cref="IMessageMetadataBuilder"/>, e.g. mid fluent chain) MUST
/// <b>accumulate</b> into the single dict-typed <c>Attributes</c>/<c>Items</c> bag that
/// <see cref="MetadataCollectionExtensions.GetAttributes"/>/<see cref="MetadataCollectionExtensions.GetItems"/>
/// read — never replace-as-raw-array.
/// </summary>
/// <remarks>
/// <para>
/// Surfaced by the <c>az9u1e</c> round-trip lock and ruled by SoftwareArchitect (17003): <c>Add*</c> means
/// accumulate (BCL convention), with ONE storage representation (the canonical <c>_attributes</c>/<c>_items</c>
/// dict that <c>GetAttributes()</c>/<c>GetItems()</c> read). The pre-fix extension routed through
/// <c>MessageMetadataBuilder.WithProperty</c>, which <c>.Clear()</c>'d the dict before adding (replace
/// semantics — confirmed root cause, BackendDeveloper 17021) — so each chained extension <c>AddAttribute</c>
/// wiped the prior entry and only the last survived. The fix (BackendDeveloper, single-owner Abstractions)
/// removes the <c>.Clear()</c> so <c>Add*</c> merges into the single dict; a true replace, if ever wanted,
/// is a separate <c>WithAttributes</c>/<c>SetAttributes</c> verb. This lock authored independently (author≠impl).
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix replace-on-every-Add leaves only the LAST
/// entry, so two <c>AddAttribute</c> calls yield a 1-entry <c>GetAttributes()</c> — every count/merge
/// assertion below fails. GREEN once the extension merges into the single dict.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MetadataBuilderCollectionExtensionsShould
{
	// Typed as the INTERFACE so the extension Add* methods are the resolved overloads (the concrete
	// MessageMetadataBuilder.AddAttribute/AddItem convenience methods are not on IMessageMetadataBuilder).
	private static IMessageMetadataBuilder NewBuilder() =>
		new MessageMetadataBuilder()
			.WithMessageId("msg-yhoc4c")
			.WithCorrelationId("corr-yhoc4c")
			.WithMessageType("OrderPlaced")
			.WithContentType("application/json");

	[Fact]
	public void AccumulateRepeatedAddAttributeIntoGetAttributes()
	{
		var builder = NewBuilder();
		_ = builder.AddAttribute("attr-1", "v1");
		_ = builder.AddAttribute("attr-2", "v2");

		var attributes = builder.Build().GetAttributes();

		attributes.Count.ShouldBe(2);
		attributes.ShouldContainKeyAndValue("attr-1", "v1");
		attributes.ShouldContainKeyAndValue("attr-2", "v2");
	}

	[Fact]
	public void AccumulateRepeatedAddItemIntoGetItems()
	{
		var builder = NewBuilder();
		_ = builder.AddItem("item-1", "v1");
		_ = builder.AddItem("item-2", "v2");

		var items = builder.Build().GetItems();

		items.Count.ShouldBe(2);
		items.ShouldContainKeyAndValue("item-1", "v1");
		items.ShouldContainKeyAndValue("item-2", "v2");
	}

	[Fact]
	public void MergeAddAttributesWithPriorAddAttribute_NotReplace()
	{
		var builder = NewBuilder();
		_ = builder.AddAttribute("attr-1", "v1");
		_ = builder.AddAttributes(
		[
			new KeyValuePair<string, object>("attr-2", "v2"),
			new KeyValuePair<string, object>("attr-3", "v3"),
		]);

		var attributes = builder.Build().GetAttributes();

		// "Add" accumulates — the prior attr-1 survives alongside the batch (a replace would drop attr-1).
		attributes.Count.ShouldBe(3);
		attributes.ShouldContainKeyAndValue("attr-1", "v1");
		attributes.ShouldContainKeyAndValue("attr-2", "v2");
		attributes.ShouldContainKeyAndValue("attr-3", "v3");
	}

	[Fact]
	public void MergeAddItemsWithPriorAddItem_NotReplace()
	{
		var builder = NewBuilder();
		_ = builder.AddItem("item-1", "v1");
		_ = builder.AddItems(
		[
			new KeyValuePair<string, object>("item-2", "v2"),
			new KeyValuePair<string, object>("item-3", "v3"),
		]);

		var items = builder.Build().GetItems();

		items.Count.ShouldBe(3);
		items.ShouldContainKeyAndValue("item-1", "v1");
		items.ShouldContainKeyAndValue("item-2", "v2");
		items.ShouldContainKeyAndValue("item-3", "v3");
	}
}
