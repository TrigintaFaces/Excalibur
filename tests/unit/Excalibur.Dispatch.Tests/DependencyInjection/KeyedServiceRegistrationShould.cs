// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - needed for test fixture types

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Independent regression lock for <c>bd-wl9s4v</c> (keyed-DI handling): the Dispatch-core registration
/// paths enumerate the whole <see cref="IServiceCollection"/> and read <c>descriptor.ImplementationType</c>
/// / <c>ImplementationInstance</c>. On a keyed descriptor those non-keyed accessors do NOT expose the
/// implementation — the implementation lives on <c>KeyedImplementationType</c>/<c>KeyedImplementationInstance</c>.
/// The fix reads the keyed accessor under <c>IsKeyedService</c>.
/// </summary>
/// <remarks>
/// <para>
/// Author≠implementer (BackendDeveloper owns the source fix; this is the independent lock).
/// </para>
/// <para>
/// <b>Runtime-version nuance (verified by RED-proof + Microsoft docs / dotnet/runtime#95789):</b> on the
/// <c>Microsoft.Extensions.DependencyInjection</c> 8.x runtime the non-keyed <c>ImplementationType</c>
/// getter <i>throws</i> <see cref="InvalidOperationException"/> for a keyed descriptor (a hard crash when
/// any keyed service is registered). On 9.x/10.x (this test runtime targets <c>net10.0</c>) the getter
/// was changed to return <see langword="null"/> instead, so the pre-fix bug manifests as a <b>silent
/// skip</b>: keyed handlers are never indexed/promoted/discovered. These locks therefore assert the
/// <b>behavioral outcome</b> (the keyed handler IS indexed / promoted / discovered, key preserved) rather
/// than "does not throw" — that assertion would be vacuous on the 9.x/10.x runtime. Each test is RED on
/// the pre-fix code (skip → not indexed/promoted/discovered) and GREEN on the fix, on every supported
/// runtime.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KeyedServiceRegistrationShould
{
	// Site: HandlerLifetimeRegistry.Build (lazy, runs on first dispatch). It indexes the implementation
	// type of every descriptor; pre-fix a keyed handler's impl type is invisible, so a lifetime lookup
	// by the concrete keyed handler type fails.
	[Fact]
	public void IndexKeyedHandlerImplementationTypeForLifetimeLookup()
	{
		// Arrange — a keyed handler registered by interface.
		var services = new ServiceCollection();
		_ = services.AddKeyedTransient<IActionHandler<KeyedTestCommand>, KeyedTestCommandHandler>("key-1");

		var registry = new HandlerLifetimeRegistry(services);

		// Act — the map is built lazily on first lookup; look up by the concrete (keyed) impl type.
		var found = registry.TryGetLifetime(typeof(KeyedTestCommandHandler), out var lifetime);

		// Assert — pre-fix the keyed impl type is null → never indexed → found == false.
		found.ShouldBeTrue();
		lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	// Site: HandlerLifetimeAnalyzer.PromoteEligibleHandlers outer loop. Pre-fix the keyed handler's impl
	// type is invisible → it is silently NOT promoted. Also locks key preservation on promote.
	[Fact]
	public void PromoteKeyedHandlerAndPreserveServiceKey()
	{
		// Arrange — a keyed, promotable (stateless, no deps) handler registered by interface.
		var services = new ServiceCollection();
		_ = services.AddKeyedTransient<IActionHandler<KeyedTestCommand>, KeyedTestCommandHandler>("handler-key");

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — pre-fix promoted == 0 (keyed handler skipped); post-fix promoted and key preserved.
		promoted.ShouldBeGreaterThanOrEqualTo(1);

		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<KeyedTestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		descriptor.IsKeyedService.ShouldBeTrue();
		descriptor.ServiceKey.ShouldBe("handler-key");
	}

	// Site: HandlerLifetimeAnalyzer.PromoteEligibleHandlers INNER promote loop, which promotes the concrete
	// self-registration of a promoted handler. Pre-fix the keyed concrete registration's impl type is
	// invisible → the inner match fails → it is not promoted (stays Transient).
	[Fact]
	public void PromoteKeyedConcreteSelfRegistrationInInnerLoop()
	{
		// Arrange — keyed handler by interface + a keyed concrete self-registration (both transient).
		var services = new ServiceCollection();
		_ = services.AddKeyedTransient<IActionHandler<KeyedTestCommand>, KeyedTestCommandHandler>("inner-key");
		_ = services.AddKeyedTransient<KeyedTestCommandHandler, KeyedTestCommandHandler>("inner-key");

		// Act
		_ = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — the concrete keyed self-registration is promoted to a keyed singleton.
		var concrete = services.First(d => d.ServiceType == typeof(KeyedTestCommandHandler));
		concrete.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		concrete.IsKeyedService.ShouldBeTrue();
	}

	// Public seam (SA-pinned): AddDispatch over a collection containing a keyed handler, then resolve
	// IHandlerRegistry — its factory enumerates handler-interface descriptors. Pre-fix the keyed handler
	// is silently skipped and never discoverable for dispatch.
	[Fact]
	public void DiscoverKeyedHandlerThroughAddDispatchHandlerRegistry()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddKeyedTransient<IActionHandler<KeyedTestCommand>, KeyedTestCommandHandler>("dispatch-key");
		_ = services.AddDispatch();

		using var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetRequiredService<IHandlerRegistry>();
		var discovered = registry.TryGetHandler(typeof(KeyedTestCommand), out var entry);

		// Assert — pre-fix the keyed handler is skipped → not discovered; post-fix it resolves.
		discovered.ShouldBeTrue();
		_ = entry.ShouldNotBeNull();
		entry.HandlerType.ShouldBe(typeof(KeyedTestCommandHandler));
	}

	#region Test Types

	public sealed class KeyedTestCommand : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string Type { get; set; } = "KeyedTestCommand";
		public string MessageType { get; set; } = "KeyedTestCommand";
		public MessageKinds Kind { get; set; } = MessageKinds.Action;
		public object Body { get; set; } = new object();
		public ReadOnlyMemory<byte> Payload { get; set; }
		public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
		public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
	}

	public sealed class KeyedTestCommandHandler : IActionHandler<KeyedTestCommand>
	{
		public Task HandleAsync(KeyedTestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
