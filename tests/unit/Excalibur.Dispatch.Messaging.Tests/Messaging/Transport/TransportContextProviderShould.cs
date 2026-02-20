// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportContextProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportContextProviderShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TransportContextProvider(null!));
	}

	[Fact]
	public void CreateInstance_WhenRegistryIsProvided()
	{
		// Arrange
		var registry = new TransportBindingRegistry();

		// Act
		var provider = new TransportContextProvider(registry);

		// Assert
		_ = provider.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region GetTransportBinding Tests

	[Fact]
	public void ReturnNull_WhenContextIsNull()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var provider = new TransportContextProvider(registry);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => provider.GetTransportBinding(null!));
	}

	[Fact]
	public void ReturnNull_WhenBindingNamePropertyNotSet()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Properties).Returns(new Dictionary<string, object?>());

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		binding.ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenBindingNameIsEmpty()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = string.Empty
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		binding.ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenBindingNotRegistered()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "unknown-binding"
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		binding.ShouldBeNull();
	}

	[Fact]
	public void ReturnBinding_WhenBindingIsRegistered()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var expectedBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => expectedBinding.Name).Returns("test-binding");
		registry.RegisterBinding(expectedBinding);

		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "test-binding"
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		_ = binding.ShouldNotBeNull();
		binding.ShouldBe(expectedBinding);
	}

	[Fact]
	public void RequireExactCaseMatch_ForBindingLookup()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var expectedBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => expectedBinding.Name).Returns("Test-Binding");
		registry.RegisterBinding(expectedBinding);

		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "test-binding" // lowercase - won't match
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert - Registry uses StringComparer.Ordinal (case-sensitive)
		binding.ShouldBeNull();
	}

	[Fact]
	public void ReturnBinding_WhenExactCaseMatches()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var expectedBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => expectedBinding.Name).Returns("Test-Binding");
		registry.RegisterBinding(expectedBinding);

		var provider = new TransportContextProvider(registry);
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "Test-Binding" // exact case - matches
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		_ = binding.ShouldNotBeNull();
		binding.ShouldBe(expectedBinding);
	}

	#endregion GetTransportBinding Tests

	#region TransportBindingNameProperty Tests

	[Fact]
	public void ExposeTransportBindingNamePropertyConstant()
	{
		// Assert
		TransportContextProvider.TransportBindingNameProperty.ShouldBe("TransportBindingName");
	}

	#endregion TransportBindingNameProperty Tests

	#region Integration with MessageContextExtensions Tests

	[Fact]
	public void WorkWithMessageContextExtensions()
	{
		// Arrange
		var registry = new TransportBindingRegistry();
		var expectedBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => expectedBinding.Name).Returns("rabbitmq");
		registry.RegisterBinding(expectedBinding);

		var provider = new TransportContextProvider(registry);

		// Create a real-ish context with properties dictionary
		var context = A.Fake<IMessageContext>();
		var properties = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "rabbitmq"
		};
		_ = A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var binding = provider.GetTransportBinding(context);

		// Assert
		_ = binding.ShouldNotBeNull();
		binding.Name.ShouldBe("rabbitmq");
	}

	#endregion Integration with MessageContextExtensions Tests

	#region Multiple Bindings Tests

	[Fact]
	public void ResolveCorrectBinding_WhenMultipleBindingsRegistered()
	{
		// Arrange
		var registry = new TransportBindingRegistry();

		var rabbitBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => rabbitBinding.Name).Returns("rabbitmq");
		registry.RegisterBinding(rabbitBinding);

		var kafkaBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => kafkaBinding.Name).Returns("kafka");
		registry.RegisterBinding(kafkaBinding);

		var serviceBusBinding = A.Fake<ITransportBinding>();
		_ = A.CallTo(() => serviceBusBinding.Name).Returns("azure-servicebus");
		registry.RegisterBinding(serviceBusBinding);

		var provider = new TransportContextProvider(registry);

		// Test kafka binding
		var kafkaContext = A.Fake<IMessageContext>();
		var kafkaProps = new Dictionary<string, object?>
		{
			[TransportContextProvider.TransportBindingNameProperty] = "kafka"
		};
		_ = A.CallTo(() => kafkaContext.Properties).Returns(kafkaProps);

		// Act
		var binding = provider.GetTransportBinding(kafkaContext);

		// Assert
		_ = binding.ShouldNotBeNull();
		binding.ShouldBe(kafkaBinding);
		binding.Name.ShouldBe("kafka");
	}

	#endregion Multiple Bindings Tests
}
