// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Locks the observable contract of <see cref="MessageContextExtensions.GetMessageType"/> now that
/// <see cref="MessageContext"/> keeps the value in a dedicated field instead of the Items dictionary.
/// The hoisting is a performance change and must be invisible: the same string comes back, for the
/// concrete context and for a foreign <see cref="IMessageContext"/> alike, the value still surfaces
/// through <see cref="IMessageContext.Items"/>, and the type reported is always the message's runtime
/// type -- never the static type it was dispatched as.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageTypeFastPathShould
{
	[Fact]
	public async Task ReportTheMessageType_AfterDispatchingOnAMessageContext()
	{
		var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new MessageContext(new BaseCommand(), provider);

		_ = await dispatcher.DispatchAsync(new BaseCommand(), context, CancellationToken.None);

		context.GetMessageType().ShouldBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
	}

	[Fact]
	public async Task ReportTheMessageType_AfterDispatchingOnAForeignMessageContext()
	{
		var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// A context implementation the framework knows nothing about must keep working off the Items
		// dictionary exactly as before -- the field only exists on MessageContext.
		IMessageContext context = new TestMessageContext();

		_ = await dispatcher.DispatchAsync(new BaseCommand(), context, CancellationToken.None);

		context.GetMessageType().ShouldBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
		context.Items[MessageTypeItemsKey].ShouldBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
	}

	[Fact]
	public async Task ReportTheDerivedType_WhenADerivedMessageIsDispatchedAsItsBase()
	{
		// The static type is deliberately the base: a dedicated field must not tempt anyone into
		// stamping typeof(TMessage), which differs from the runtime type on exactly this shape and
		// would silently route a derived message under its base name.
		var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		BaseCommand message = new DerivedCommand();
		var context = new MessageContext(message, provider);

		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		context.GetMessageType().ShouldBe(MessageTypeCache.GetTypeName(typeof(DerivedCommand)));
		context.GetMessageType().ShouldNotBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
	}

	[Fact]
	public async Task ReportTheDerivedType_WhenADerivedMessageIsDispatchedAsItsBaseOnAForeignContext()
	{
		var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		BaseCommand message = new DerivedCommand();
		IMessageContext context = new TestMessageContext();

		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		context.GetMessageType().ShouldBe(MessageTypeCache.GetTypeName(typeof(DerivedCommand)));
		context.GetMessageType().ShouldNotBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
	}

	[Fact]
	public async Task SurfaceTheMessageTypeThroughItems_WhenTheDictionaryIsMaterializedAfterDispatch()
	{
		// Transports and telemetry enumerate Items to build headers. Moving the value into a field
		// must not remove it from that enumeration.
		var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new MessageContext(new BaseCommand(), provider);

		_ = await dispatcher.DispatchAsync(new BaseCommand(), context, CancellationToken.None);

		context.Items[MessageTypeItemsKey].ShouldBe(MessageTypeCache.GetTypeName(typeof(BaseCommand)));
		context.Items[MessageTypeIsRoutingDefaultItemsKey].ShouldBe(true);
	}

	[Fact]
	public void PreferAnItemsEntryWrittenDirectly_OverTheField()
	{
		// The dictionary stays authoritative: anything that writes the well-known key by hand keeps
		// winning, which is what it did before the value moved to a field.
		var context = new MessageContext();
		context.SetMessageType("from-the-field");
		context.Items[MessageTypeItemsKey] = "from-the-dictionary";

		context.GetMessageType().ShouldBe("from-the-dictionary");
	}

	[Fact]
	public void KeepItemsInStep_WhenTheMessageTypeIsSetAfterTheDictionaryExists()
	{
		var context = new MessageContext();
		_ = context.Items.Count;

		context.SetMessageType("set-after-materialization");

		context.GetMessageType().ShouldBe("set-after-materialization");
		context.Items[MessageTypeItemsKey].ShouldBe("set-after-materialization");
	}

	[Fact]
	public void ReportNoMessageType_WhenItIsSetToNull_WhicheverSideOfMaterializationTheCallLandsOn()
	{
		// Setting the type to null is reachable -- a context validator exercises exactly this -- and the
		// hoisting made the two sides of materialization behave differently in the dictionary: before,
		// every set went through Items and left the key present holding null; now an unmaterialized
		// context leaves no key at all. GetMessageType answers null either way, and that is the part
		// callers depend on, so it is asserted on BOTH sides rather than on whichever one the
		// implementation happens to take.
		var neverMaterialized = new MessageContext();
		neverMaterialized.SetMessageType("replaced-by-null");
		neverMaterialized.SetMessageType(null);

		var alreadyMaterialized = new MessageContext();
		_ = alreadyMaterialized.Items.Count;
		alreadyMaterialized.SetMessageType("replaced-by-null");
		alreadyMaterialized.SetMessageType(null);

		neverMaterialized.GetMessageType().ShouldBeNull();
		alreadyMaterialized.GetMessageType().ShouldBeNull();

		// And the value must not come back when the dictionary is materialized later: the seed on first
		// materialization is what surfaces the field to transports, so a null that seeded a stale value
		// would put a message type back on the wire after it had been cleared.
		neverMaterialized.Items.ContainsKey(MessageTypeItemsKey).ShouldBeFalse(
			"a message type cleared to null must not reappear when Items is materialized afterwards");
	}

	[Fact]
	public void ClearTheMessageType_WhenTheContextIsReset()
	{
		// Contexts are pooled. A stale type surviving Reset would mislabel the next message.
		var context = new MessageContext();
		context.SetMessageType("stale");
		context.MarkMessageTypeAsRoutingDefault();

		context.Reset();

		context.GetMessageType().ShouldBeNull();
		context.IsMessageTypeRoutingDefault().ShouldBeFalse();
	}

	[Fact]
	public void ReportTheRoutingDefaultMarker_OnAMessageContextAndOnAForeignContext()
	{
		var concrete = new MessageContext();
		IMessageContext foreign = new TestMessageContext();

		concrete.IsMessageTypeRoutingDefault().ShouldBeFalse();
		foreign.IsMessageTypeRoutingDefault().ShouldBeFalse();

		concrete.MarkMessageTypeAsRoutingDefault();
		foreign.MarkMessageTypeAsRoutingDefault();

		concrete.IsMessageTypeRoutingDefault().ShouldBeTrue();
		foreign.IsMessageTypeRoutingDefault().ShouldBeTrue();
	}

	private const string MessageTypeItemsKey = "__MessageType";
	private const string MessageTypeIsRoutingDefaultItemsKey = "__MessageTypeIsRoutingDefault";

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<IActionHandler<BaseCommand>, BaseCommandHandler>();
		_ = services.AddTransient<BaseCommandHandler>();
		_ = services.AddTransient<IActionHandler<DerivedCommand>, DerivedCommandHandler>();
		_ = services.AddTransient<DerivedCommandHandler>();
		_ = services.AddDispatch(configure: null);

		return services.BuildServiceProvider();
	}

	private class BaseCommand : IDispatchAction;

	private sealed class DerivedCommand : BaseCommand;

	private sealed class BaseCommandHandler : IActionHandler<BaseCommand>
	{
		public Task HandleAsync(BaseCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class DerivedCommandHandler : IActionHandler<DerivedCommand>
	{
		public Task HandleAsync(DerivedCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
