// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// A registry implemented OUTSIDE this assembly can actually be used.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this arm did not exist, which is the whole reason the defect shipped.</b> <c>IHandlerRegistry</c>
/// is public and shipped, and the bus's fallback branch for non-native registries required each entry to be
/// the <c>internal sealed</c> <c>HandlerRegistryEntry</c> — a type no implementation outside this assembly
/// can construct. So the branch written to serve foreign registries rejected everything a foreign registry
/// can produce, and threw <see cref="InvalidCastException"/> at dispatch rather than at registration.
/// </para>
/// <para>
/// The suite could not see it because every existing double is a <c>FakeItEasy</c> fake of
/// <c>IHandlerRegistry</c> that hands back <c>HandlerRegistryEntry</c> instances — the native type, which
/// the test assembly can reach through <c>InternalsVisibleTo</c> and a consumer cannot. <b>The doubles were
/// more capable than any real implementor</b>, so the cast always succeeded and the impossible path was
/// never taken.
/// </para>
/// <para>
/// The registry below therefore implements the interface from scratch and returns its OWN
/// <see cref="IHandlerRegistryEntry"/> implementation. That is the only shape a consumer could write, and
/// it is the shape nothing in the corpus had.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ForeignHandlerRegistryShould
{
	/// <summary>
	/// SAFETY. Dispatching through a registry this assembly did not build must not throw. RED before the
	/// fix with <see cref="InvalidCastException"/>, from a branch whose comment said foreign registries
	/// were supported.
	/// </summary>
	[Fact]
	public async Task NotThrowWhenTheRegistryWasImplementedOutsideThisAssembly()
	{
		var bus = BusOver(new ForeignRegistry(typeof(ForeignEvent), typeof(ForeignHandler)));

		var publish = async () => await bus.PublishAsync(
			new ForeignEvent(),
			MessageContext(),
			TestContext.Current.CancellationToken);

		await Should.NotThrowAsync(publish);
	}

	/// <summary>
	/// LIVENESS. Without this, the arm above is satisfied by a bus that silently resolves no handlers at
	/// all — which throws nothing and does nothing, and would pass while the extension point stayed dead.
	/// The entries the foreign registry supplied must actually come back through the interface.
	/// </summary>
	[Fact]
	public void StillReadTheEntriesAForeignRegistrySupplied()
	{
		IHandlerRegistry foreign = new ForeignRegistry(typeof(ForeignEvent), typeof(ForeignHandler));

		var all = foreign.GetAll();

		all.Count.ShouldBe(1);
		all[0].MessageType.ShouldBe(typeof(ForeignEvent));
		all[0].HandlerType.ShouldBe(typeof(ForeignHandler));
		all[0].ShouldNotBeOfType<HandlerRegistryEntry>(
			"the point of this fixture is that it does NOT produce the framework's internal entry type — "
			+ "if it ever does, this suite stops testing what a consumer can actually write");
	}

	private static LocalMessageBus BusOver(IHandlerRegistry registry)
	{
		// The activator must return a REAL handler instance. A bare fake returns a Castle proxy, which the
		// bus then cannot cast to the declared handler type -- an InvalidCastException from the FIXTURE
		// that looks exactly like the one this arm exists to detect. Two different defects with the same
		// exception type is precisely how an arm stops discriminating, so the double is pinned here.
		var activator = A.Fake<IHandlerActivator>();
		A.CallTo(() => activator.ActivateHandler(typeof(ForeignHandler), A<IMessageContext>._, A<IServiceProvider>._))
			.Returns(new ForeignHandler());

		return new LocalMessageBus(
			A.Fake<IServiceProvider>(),
			registry,
			activator,
			A.Fake<IHandlerInvoker>(),
			A.Fake<ILogger<LocalMessageBus>>());
	}

	private static IMessageContext MessageContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		return context;
	}

	/// <summary>
	/// A registry written the way a consumer would have to write one: implementing only the public
	/// interface, and producing only the public entry interface.
	/// </summary>
	private sealed class ForeignRegistry(Type messageType, Type handlerType) : IHandlerRegistry
	{
		private readonly List<IHandlerRegistryEntry> _entries =
			[new ForeignEntry(messageType, handlerType)];

		public void Register(Type messageType, Type handlerType, bool expectsResponse, Type? responseType = null) =>
			_entries.Add(new ForeignEntry(messageType, handlerType));

		public bool TryGetHandler(Type messageType, out IHandlerRegistryEntry entry)
		{
			foreach (var candidate in _entries)
			{
				if (candidate.MessageType == messageType)
				{
					entry = candidate;
					return true;
				}
			}

			entry = null!;
			return false;
		}

		public IReadOnlyList<IHandlerRegistryEntry> GetAll() => _entries;
	}

	/// <summary>
	/// The entry type a consumer would supply: the public interface, and nothing of ours.
	/// </summary>
	private sealed class ForeignEntry(Type messageType, Type handlerType) : IHandlerRegistryEntry
	{
		public Type MessageType { get; } = messageType;

		public Type HandlerType { get; } = handlerType;

		public bool ExpectsResponse => false;

		public Type? ResponseType => null;
	}

	private sealed class ForeignEvent : IDispatchEvent;

	private sealed class ForeignHandler : IEventHandler<ForeignEvent>
	{
		public Task HandleAsync(ForeignEvent eventMessage, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}
}
