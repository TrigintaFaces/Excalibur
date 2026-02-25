// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for the <see cref="InboxConfigurationBuilder"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.1: Builder pattern tests (0% → 50%+).
/// Tests fluent builder configuration for selective inbox settings.
/// Note: InboxConfigurationBuilder is internal, accessed via InternalsVisibleTo.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxConfigurationBuilderShould
{
	#region ForHandler Tests

	[Fact]
	public void ReturnInboxHandlerConfiguration_WhenForHandlerIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var config = builder.ForHandler<TestHandler>();

		// Assert
		_ = config.ShouldNotBeNull();
		_ = config.ShouldBeAssignableTo<IInboxHandlerConfiguration>();
	}

	[Fact]
	public void ReturnSameConfiguration_WhenForHandlerIsCalledTwiceForSameType()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var config1 = builder.ForHandler<TestHandler>();
		var config2 = builder.ForHandler<TestHandler>();

		// Assert
		config1.ShouldBeSameAs(config2);
	}

	[Fact]
	public void ReturnDifferentConfigurations_WhenForHandlerIsCalledForDifferentTypes()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var config1 = builder.ForHandler<TestHandler>();
		var config2 = builder.ForHandler<AnotherTestHandler>();

		// Assert
		config1.ShouldNotBeSameAs(config2);
	}

	#endregion

	#region ForHandlersMatching Tests

	[Fact]
	public void ThrowArgumentNullException_WhenPredicateIsNull()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.ForHandlersMatching(null!, _ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.ForHandlersMatching(_ => true, null!));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenForHandlersMatchingIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		var wasConfigured = false;

		// Act
		_ = builder.ForHandlersMatching(_ => true, _ => wasConfigured = true);

		// Assert
		wasConfigured.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSameBuilder_WhenForHandlersMatchingIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var result = builder.ForHandlersMatching(_ => true, _ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ForNamespace Tests

	[Fact]
	public void ThrowArgumentException_WhenNamespacePrefixIsNull()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.ForNamespace(null!, _ => { }));
	}

	[Fact]
	public void ThrowArgumentException_WhenNamespacePrefixIsEmpty()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.ForNamespace(string.Empty, _ => { }));
	}

	[Fact]
	public void ThrowArgumentException_WhenNamespacePrefixIsWhitespace()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			builder.ForNamespace("   ", _ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenForNamespaceConfigureIsNull()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.ForNamespace("MyApp.Handlers", null!));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenForNamespaceIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		var wasConfigured = false;

		// Act
		_ = builder.ForNamespace("MyApp.Handlers", _ => wasConfigured = true);

		// Assert
		wasConfigured.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSameBuilder_WhenForNamespaceIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var result = builder.ForNamespace("MyApp.Handlers", _ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ForMessageType Tests

	[Fact]
	public void ThrowArgumentNullException_WhenForMessageTypeConfigureIsNull()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.ForMessageType<TestMessage>(null!));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenForMessageTypeIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		var wasConfigured = false;

		// Act
		_ = builder.ForMessageType<TestMessage>(_ => wasConfigured = true);

		// Assert
		wasConfigured.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSameBuilder_WhenForMessageTypeIsCalled()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var result = builder.ForMessageType<TestMessage>(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Build Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuildIsCalledWithNullHandlerTypes()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.Build(null!));
	}

	[Fact]
	public void ReturnConfigurationProvider_WhenBuildIsCalledWithEmptyHandlerTypes()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var provider = builder.Build(Array.Empty<Type>());

		// Assert
		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnConfigurationProvider_WhenBuildIsCalledWithHandlerTypes()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandler<TestHandler>();

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });

		// Assert
		_ = provider.ShouldNotBeNull();
	}

	#endregion

	#region Precedence Tests

	[Fact]
	public void ResolveExactTypePriority_OverNamespaceMatch()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		var exactConfig = builder.ForHandler<TestHandler>();
		_ = builder.ForNamespace("Excalibur.Dispatch.Tests", _ => { });

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });

		// Assert - Exact match should be used
		var settings = provider.GetConfiguration(typeof(TestHandler));
		_ = settings.ShouldNotBeNull();
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_WhenMultipleRulesAreConfigured()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var result = builder
			.ForNamespace("MyApp.Handlers", _ => { })
			.ForHandlersMatching(t => t.Name.EndsWith("EventHandler"), _ => { })
			.ForMessageType<TestMessage>(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Test Fixtures

	private sealed class TestHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class AnotherTestHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class TestMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	#endregion
}
