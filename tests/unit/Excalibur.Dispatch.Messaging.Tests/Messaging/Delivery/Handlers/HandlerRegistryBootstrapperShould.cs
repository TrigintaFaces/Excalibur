// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for the <see cref="HandlerRegistryBootstrapper"/> class.
/// </summary>
/// <remarks>
/// Sprint 413 - Task T413.3: HandlerRegistryBootstrapper tests (61.9% → 85%).
/// Sprint 452 - Updated to reflect source-generator-first architecture.
/// Tests the bootstrapping strategy: precompiled registry is always used first.
/// Assembly scanning is only a fallback when precompiled registry doesn't exist.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerRegistryBootstrapperShould
{
	#region Precompiled Registry Tests (Sprint 452 - Source Generators Enabled by Default)

	/// <summary>
	/// Verifies that Bootstrap uses the PrecompiledHandlerRegistry when available.
	/// With source generators enabled by default, this is the primary code path.
	/// </summary>
	[Fact]
	public void UsePrecompiledRegistry_WhenAvailable()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = Array.Empty<Assembly>(); // No fallback needed

		// Act - Bootstrap should use precompiled registry
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Assert - Should complete without error (precompiled registry was used)
		// Note: The Dispatch library project has no handlers, so HandlerCount is 0
		// This test verifies the bootstrapper path, not handler discovery
		_ = registry.GetAll().ShouldNotBeNull();
	}

	/// <summary>
	/// Verifies that the precompiled registry count matches expectations.
	/// The Dispatch framework project itself has no handler implementations.
	/// </summary>
	[Fact]
	public void PrecompiledRegistryHasExpectedHandlerCount()
	{
		// Assert - The Dispatch project's PrecompiledHandlerRegistry has 0 handlers
		// because it's a framework library, not an application with handlers
		PrecompiledHandlerRegistry.HandlerCount.ShouldBe(0);
	}

	#endregion

	#region Assembly Scanning Fallback Tests

	/// <summary>
	/// Tests that Bootstrap completes without error when given assemblies.
	/// With precompiled registry available, fallback assemblies are ignored.
	/// </summary>
	[Fact]
	public void CompleteWithoutError_WhenGivenFallbackAssemblies()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = new[] { typeof(HandlerRegistryBootstrapperShould).Assembly };

		// Act - Bootstrap with this test assembly as fallback
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Assert - Should have completed without error
		// Note: With precompiled registry available, fallback is not used
		_ = registry.GetAll().ShouldNotBeNull();
	}

	/// <summary>
	/// Tests that multiple fallback assemblies are handled gracefully.
	/// </summary>
	[Fact]
	public void HandleMultipleFallbackAssemblies_WithoutError()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = new[]
		{
			typeof(HandlerRegistryBootstrapperShould).Assembly,
			typeof(IDispatchMessage).Assembly, // Excalibur.Dispatch.Abstractions assembly
		};

		// Act
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Assert - Should complete without error
		_ = registry.GetAll().ShouldNotBeNull();
	}

	/// <summary>
	/// Tests that empty fallback assemblies are handled gracefully.
	/// </summary>
	[Fact]
	public void CompleteWithoutError_WhenFallbackAssembliesEmpty()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = Array.Empty<Assembly>();

		// Act & Assert - Should not throw
		Should.NotThrow(() => HandlerRegistryBootstrapper.Bootstrap(registry, assemblies));
	}

	#endregion

	#region Direct Registry Registration Tests (Testing HandlerRegistry Directly)

	/// <summary>
	/// Tests direct handler registration to verify the registry works correctly.
	/// This bypasses the bootstrapper to test the underlying registry functionality.
	/// </summary>
	[Fact]
	public void RegisterHandlersDirectly_ActionHandler()
	{
		// Arrange
		var registry = new HandlerRegistry();

		// Act - Register directly (bypassing bootstrapper)
		registry.Register(
			typeof(TestActionCommand),
			typeof(TestActionHandler),
			expectsResponse: false);

		// Assert
		var found = registry.TryGetHandler(typeof(TestActionCommand), out var entry);
		found.ShouldBeTrue();
		entry.HandlerType.ShouldBe(typeof(TestActionHandler));
		entry.ExpectsResponse.ShouldBeFalse();
	}

	/// <summary>
	/// Tests direct handler registration for query handlers (with response).
	/// </summary>
	[Fact]
	public void RegisterHandlersDirectly_QueryHandler()
	{
		// Arrange
		var registry = new HandlerRegistry();

		// Act - Register directly
		registry.Register(
			typeof(TestQueryCommand),
			typeof(TestQueryHandler),
			expectsResponse: true);

		// Assert
		var found = registry.TryGetHandler(typeof(TestQueryCommand), out var entry);
		found.ShouldBeTrue();
		entry.HandlerType.ShouldBe(typeof(TestQueryHandler));
		entry.ExpectsResponse.ShouldBeTrue();
	}

	/// <summary>
	/// Tests direct handler registration for event handlers.
	/// </summary>
	[Fact]
	public void RegisterHandlersDirectly_EventHandler()
	{
		// Arrange
		var registry = new HandlerRegistry();

		// Act - Register directly
		registry.Register(
			typeof(TestDomainEvent),
			typeof(TestEventHandler),
			expectsResponse: false);

		// Assert
		var found = registry.TryGetHandler(typeof(TestDomainEvent), out var entry);
		found.ShouldBeTrue();
		entry.HandlerType.ShouldBe(typeof(TestEventHandler));
		entry.ExpectsResponse.ShouldBeFalse();
	}

	/// <summary>
	/// Tests direct handler registration for document handlers.
	/// </summary>
	[Fact]
	public void RegisterHandlersDirectly_DocumentHandler()
	{
		// Arrange
		var registry = new HandlerRegistry();

		// Act - Register directly
		registry.Register(
			typeof(TestDocument),
			typeof(TestDocumentHandler),
			expectsResponse: false);

		// Assert
		var found = registry.TryGetHandler(typeof(TestDocument), out var entry);
		found.ShouldBeTrue();
		entry.HandlerType.ShouldBe(typeof(TestDocumentHandler));
		entry.ExpectsResponse.ShouldBeFalse();
	}

	#endregion

	#region Type Filtering Tests

	/// <summary>
	/// Tests that abstract handler classes are not present in registry after bootstrap.
	/// </summary>
	[Fact]
	public void RegistryDoesNotContainAbstractHandlers()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = new[] { typeof(AbstractActionHandler).Assembly };

		// Act
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Assert - Abstract handler should NOT be registered
		var allHandlers = registry.GetAll();
		allHandlers.ShouldNotContain(e => e.HandlerType == typeof(AbstractActionHandler));
	}

	/// <summary>
	/// Tests that interface types don't cause errors during bootstrap.
	/// </summary>
	[Fact]
	public void HandleInterfaceTypes_WithoutError()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = new[] { typeof(IActionHandler<>).Assembly };

		// Act
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Assert - Interface types should NOT cause errors
		_ = registry.GetAll().ShouldNotBeNull();
	}

	/// <summary>
	/// Tests that multiple handlers can be registered in the same registry.
	/// </summary>
	[Fact]
	public void RegisterMultipleHandlersDirectly()
	{
		// Arrange
		var registry = new HandlerRegistry();

		// Act - Register multiple handlers directly
		registry.Register(typeof(TestActionCommand), typeof(TestActionHandler), expectsResponse: false);
		registry.Register(typeof(TestQueryCommand), typeof(TestQueryHandler), expectsResponse: true);
		registry.Register(typeof(TestDomainEvent), typeof(TestEventHandler), expectsResponse: false);
		registry.Register(typeof(TestDocument), typeof(TestDocumentHandler), expectsResponse: false);

		// Assert - Should find all handlers
		var handlers = registry.GetAll();
		handlers.Count.ShouldBeGreaterThanOrEqualTo(4);
	}

	#endregion

	#region Edge Case Tests

	/// <summary>
	/// Tests that assemblies with no handlers don't cause errors.
	/// </summary>
	[Fact]
	public void HandleAssemblyWithNoHandlers_WithoutError()
	{
		// Arrange
		var registry = new HandlerRegistry();
		// Use mscorlib which has no message handlers
		var assemblies = new[] { typeof(string).Assembly };

		// Act & Assert - Should not throw
		Should.NotThrow(() => HandlerRegistryBootstrapper.Bootstrap(registry, assemblies));
	}

	/// <summary>
	/// Tests that duplicate assemblies in fallback list are handled gracefully.
	/// </summary>
	[Fact]
	public void HandleDuplicateAssemblies_WithoutError()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var testAssembly = typeof(TestActionHandler).Assembly;
		var assemblies = new[] { testAssembly, testAssembly }; // Duplicate

		// Act & Assert - Should not throw, should handle duplicate gracefully
		Should.NotThrow(() => HandlerRegistryBootstrapper.Bootstrap(registry, assemblies));
	}

	/// <summary>
	/// Tests the complete bootstrap and registration sequence.
	/// </summary>
	[Fact]
	public void CompleteBootstrapAndRegistrationSequence()
	{
		// Arrange
		var registry = new HandlerRegistry();
		var assemblies = new[] { typeof(TestActionHandler).Assembly };

		// Act - Bootstrap first (uses precompiled registry)
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Then register test handlers directly (simulating what an app would do)
		registry.Register(typeof(TestActionCommand), typeof(TestActionHandler), expectsResponse: false);
		registry.Register(typeof(TestQueryCommand), typeof(TestQueryHandler), expectsResponse: true);
		registry.Register(typeof(TestDomainEvent), typeof(TestEventHandler), expectsResponse: false);
		registry.Register(typeof(TestDocument), typeof(TestDocumentHandler), expectsResponse: false);

		// Assert - Verify we can use the registry after bootstrap
		registry.TryGetHandler(typeof(TestActionCommand), out var actionEntry).ShouldBeTrue();
		registry.TryGetHandler(typeof(TestQueryCommand), out var queryEntry).ShouldBeTrue();
		registry.TryGetHandler(typeof(TestDomainEvent), out var eventEntry).ShouldBeTrue();
		registry.TryGetHandler(typeof(TestDocument), out var documentEntry).ShouldBeTrue();

		actionEntry.ExpectsResponse.ShouldBeFalse();
		queryEntry.ExpectsResponse.ShouldBeTrue();
		eventEntry.ExpectsResponse.ShouldBeFalse();
		documentEntry.ExpectsResponse.ShouldBeFalse();
	}

	#endregion

	#region Test Fixtures - Handler Types

	// These handlers are discovered by HandlerRegistryBootstrapper when scanning this test assembly
	// They implement the correct interfaces with proper type constraints

#pragma warning disable CA1034 // Nested types should not be visible - needed for assembly scanning in tests

	public sealed class TestActionCommand : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionCommand";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestActionHandler : IActionHandler<TestActionCommand>
	{
		public Task HandleAsync(TestActionCommand action, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	public sealed class TestQueryResult { }

	public sealed class TestQueryCommand : IDispatchAction<TestQueryResult>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestQueryCommand";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestQueryHandler : IActionHandler<TestQueryCommand, TestQueryResult>
	{
		public Task<TestQueryResult> HandleAsync(TestQueryCommand action, CancellationToken cancellationToken)
			=> Task.FromResult(new TestQueryResult());
	}

	public sealed class TestDomainEvent : IDispatchEvent
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Event;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDomainEvent";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestEventHandler : IEventHandler<TestDomainEvent>
	{
		public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	public sealed class TestDocument : IDispatchDocument
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Document;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDocument";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

		public string DocumentId { get; set; } = Guid.NewGuid().ToString();
		public string DocumentType { get; set; } = "Test";
		public string? ContentType { get; set; } = "application/json";
	}

	public sealed class TestDocumentHandler : IDocumentHandler<TestDocument>
	{
		public Task HandleAsync(TestDocument document, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	public abstract class AbstractActionHandler : IActionHandler<TestActionCommand>
	{
		public abstract Task HandleAsync(TestActionCommand action, CancellationToken cancellationToken);
	}

#pragma warning restore CA1034

	#endregion
}
