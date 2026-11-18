// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// Locks the projection of message-context items onto CloudEvents extension attributes, for both writers
/// that perform it.
/// </summary>
/// <remarks>
/// The propagation surface takes any key a consumer chooses, so a writer cannot assume anything about the
/// keys it is handed. These arms bind the rules at the writer rather than at whatever produced the
/// values: a value marked directly by a consumer is subject to the same specification as one the
/// framework marked.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CloudEventItemAttributesShould
{
	private const string DeclaredName = "contoso.orders.order-placed.v1";

	[Fact]
	public void NotThrowOnAnItemKeyThatIsNotALegalAttributeName()
	{
		// A CloudEvents attribute name is lowercase ASCII letters and digits only, and the CNCF SDK
		// refuses to construct anything else. One writer put the raw item key straight onto the event,
		// so an item named for an HTTP header did not produce a badly-named attribute -- it threw while
		// encoding a message that was otherwise fine.
		var context = Context(("X-Request-Id", "req-1"), ("a.b", "v"), ("Authorization", "Bearer t"));

		var cloudEvent = Should.NotThrow(() => new OrderPlaced().ToCloudEvent(context));

		cloudEvent.ShouldNotBeNull();
	}

	[Fact]
	public void NotOverwriteAnAttributeTheEncoderAlreadySetFromTheContext()
	{
		// correlationid is set from the context, and items were applied afterwards, so an item whose
		// sanitized name collided replaced it -- silently, and with a value chosen by whoever populated
		// the item rather than by the correlation the message was dispatched under.
		var context = Context(("correlationid", "an-item-supplied-value"));
		context.CorrelationId = "the-real-correlation";

		var cloudEvent = new OrderPlaced().ToCloudEvent(context);

		cloudEvent["correlationid"].ShouldBe("the-real-correlation");
	}

	[Fact]
	public void ResolveACollisionTheSameWayWhateverOrderTheItemsWereAddedIn()
	{
		// Two keys that reduce to the same attribute name collide, and the winner is whichever is
		// applied last. The item collection is a dictionary, so without a defined order the winner is a
		// function of insertion order: the same message encoded by two callers who populated the context
		// in a different sequence would carry DIFFERENT VALUES under the same attribute name, and
		// nothing downstream could tell which it had.
		//
		// This asserts the VALUE, not the attribute-name set. An earlier version of this arm compared
		// only the names -- which are identical under both orders -- so it passed while the ordering
		// guarantee was removed. The names were never the property at risk.
		var addedOneWay = new OrderPlaced().ToCloudEvent(
			Context(("alpha", "first-insertion"), ("beta", "2"), ("al-pha", "second-insertion")));
		var addedTheOther = new OrderPlaced().ToCloudEvent(
			Context(("al-pha", "second-insertion"), ("beta", "2"), ("alpha", "first-insertion")));

		addedTheOther["alpha"].ShouldBe(addedOneWay["alpha"]);

		// Ordinal order puts "al-pha" before "alpha", so the un-hyphenated key is applied last and wins.
		addedOneWay["alpha"].ShouldBe("first-insertion");
	}

	[Fact]
	public void StillEmitAnOrdinaryItemAsAnExtensionAttribute()
	{
		// Liveness. Every arm above is satisfied by a projection that emits nothing at all, and that
		// would silently drop the metadata consumers rely on while every safety assertion passed.
		var cloudEvent = new OrderPlaced().ToCloudEvent(Context(("tenantroute", "eu-west")));

		cloudEvent["tenantroute"].ShouldBe("eu-west");
	}

	private static MessageContext Context(params (string Key, string Value)[] items)
	{
		var context = new MessageContext();
		foreach (var (key, value) in items)
		{
			context.SetPropagatedItem(key, value);
		}

		return context;
	}

	[Fact]
	public void NotEmitARequestLocalItemAsAnExtensionAttribute()
	{
		// The absence half. Every arm above binds the PROJECTION of a marked value; none of them would
		// notice if the request-local collection were also being projected -- which is what shipped, and
		// is how a captured Authorization header became a CloudEvents extension attribute on a broker.
		var context = new MessageContext { CorrelationId = "corr-1" };
		context.Items["tenantroute"] = "eu-west";
		context.Items["authorization"] = "Bearer leaked";
		context.SetPropagatedItem("markedroute", "eu-west");

		var cloudEvent = new OrderPlaced().ToCloudEvent(context);

		var emitted = cloudEvent.GetPopulatedAttributes().Select(a => a.Key.Name).ToList();
		emitted.ShouldNotContain("tenantroute");
		emitted.ShouldNotContain("authorization");

		// Liveness in the same arm, so it cannot pass by the encoder writing nothing at all: the
		// attributes the encoder owns, and a value the application actually marked, are both there.
		emitted.ShouldContain("correlationid");
		emitted.ShouldContain("markedroute");
	}

	[MessageName(DeclaredName)]
	private sealed record OrderPlaced : IDispatchEvent;
}
