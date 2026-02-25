// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineBuilder"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Configuration")]
public sealed class PipelineBuilderShould
{
	private readonly IServiceProvider _serviceProvider;
	private readonly PipelineBuilder _sut;

	public PipelineBuilderShould()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IPipelineProfileRegistry, PipelineProfileRegistry>();
		services.AddScoped<TestMiddleware>();
		services.AddScoped<AnotherTestMiddleware>();
		_serviceProvider = services.BuildServiceProvider();
		_sut = new PipelineBuilder("Test", _serviceProvider);
	}

	[Fact]
	public void ImplementIPipelineBuilder()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IPipelineBuilder>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(PipelineBuilder).IsPublic.ShouldBeTrue();
		typeof(PipelineBuilder).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineBuilder(null!, _serviceProvider));
	}

	[Fact]
	public void ThrowWhenNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineBuilder(string.Empty, _serviceProvider));
	}

	[Fact]
	public void ThrowWhenNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineBuilder("   ", _serviceProvider));
	}

	[Fact]
	public void ThrowWhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new PipelineBuilder("Test", null!));
	}

	[Fact]
	public void StoreNameProperty()
	{
		// Assert
		_sut.Name.ShouldBe("Test");
	}

	[Fact]
	public void AcceptApplicabilityStrategyInConstructor()
	{
		// Arrange
		var strategy = A.Fake<IMiddlewareApplicabilityStrategy>();

		// Act
		var builder = new PipelineBuilder("Test", _serviceProvider, strategy);

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullApplicabilityStrategy()
	{
		// Act
		var builder = new PipelineBuilder("Test", _serviceProvider, null);

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSelfFromUseGeneric()
	{
		// Act
		var result = _sut.Use<TestMiddleware>();

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ReturnSelfFromUseFactory()
	{
		// Act
		var result = _sut.Use(_ => A.Fake<IDispatchMiddleware>());

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenUseFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.Use(null!));
	}

	[Fact]
	public void ReturnSelfFromUseAt()
	{
		// Act
		var result = _sut.UseAt<TestMiddleware>(DispatchMiddlewareStage.PreProcessing);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ReturnSelfFromUseWhen()
	{
		// Act
		var result = _sut.UseWhen<TestMiddleware>(_ => true);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenUseWhenConditionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.UseWhen<TestMiddleware>(null!));
	}

	[Fact]
	public void ReturnSelfFromForMessageKinds()
	{
		// Act
		var result = _sut.ForMessageKinds(MessageKinds.Action);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ReturnSelfFromUseProfileByName()
	{
		// Act
		var result = _sut.UseProfile("Strict");

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenUseProfileNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.UseProfile((string)null!));
	}

	[Fact]
	public void ThrowWhenUseProfileNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.UseProfile(string.Empty));
	}

	[Fact]
	public void ThrowWhenUseProfileNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.UseProfile("   "));
	}

	[Fact]
	public void ThrowWhenUseProfileNameNotFound()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.UseProfile("NonExistent"));
	}

	[Fact]
	public void ThrowWhenUseProfileRegistryNotRegistered()
	{
		// Arrange
		var builder = new PipelineBuilder("Test", new ServiceCollection().BuildServiceProvider());

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			builder.UseProfile("Strict"));
	}

	[Fact]
	public void ReturnSelfFromUseProfileByInstance()
	{
		// Arrange
		var profile = new PipelineProfile("Custom", MessageKinds.All);

		// Act
		var result = _sut.UseProfile(profile);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenUseProfileInstanceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.UseProfile((IPipelineProfile)null!));
	}

	[Fact]
	public void ReturnSelfFromClear()
	{
		// Arrange
		_sut.Use<TestMiddleware>();

		// Act
		var result = _sut.Clear();

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void BuildReturnsPipeline()
	{
		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
		pipeline.ShouldBeAssignableTo<IDispatchPipeline>();
	}

	[Fact]
	public void BuildPipelineWithMiddleware()
	{
		// Arrange
		_sut.Use<TestMiddleware>();

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void BuildPipelineWithMultipleMiddleware()
	{
		// Arrange
		_sut.Use<TestMiddleware>();
		_sut.Use<AnotherTestMiddleware>();

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void BuildPipelineWithFactory()
	{
		// Arrange
		_sut.Use(sp => sp.GetRequiredService<TestMiddleware>());

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void BuildPipelineExcludesMiddlewareWhenConditionFalse()
	{
		// Arrange
		_sut.UseWhen<TestMiddleware>(_ => false);

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void BuildPipelineIncludesMiddlewareWhenConditionTrue()
	{
		// Arrange
		_sut.UseWhen<TestMiddleware>(_ => true);

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void ClearRemovesAllMiddleware()
	{
		// Arrange
		_sut.Use<TestMiddleware>();
		_sut.Use<AnotherTestMiddleware>();
		_sut.Clear();

		// Act
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void UseProfileClearsExistingMiddleware()
	{
		// Arrange
		_sut.Use<TestMiddleware>();
		var profile = new PipelineProfile("Empty", MessageKinds.All);

		// Act
		_sut.UseProfile(profile);
		var pipeline = _sut.Build();

		// Assert
		pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void AllowChainedConfiguration()
	{
		// Act
		var result = _sut
			.Use<TestMiddleware>()
			.Use<AnotherTestMiddleware>()
			.ForMessageKinds(MessageKinds.Action);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	/// <summary>
	/// Test middleware implementation.
	/// </summary>
	private sealed class TestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			nextDelegate(message, context, cancellationToken);
	}

	/// <summary>
	/// Another test middleware implementation.
	/// </summary>
	private sealed class AnotherTestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			nextDelegate(message, context, cancellationToken);
	}
}
