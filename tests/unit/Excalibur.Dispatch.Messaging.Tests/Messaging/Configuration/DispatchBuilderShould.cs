// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="DispatchBuilder"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Configuration")]
public sealed class DispatchBuilderShould : IDisposable
{
	private readonly IServiceCollection _services;
	private readonly DispatchBuilder _sut;

	public DispatchBuilderShould()
	{
		_services = new ServiceCollection();
		_sut = new DispatchBuilder(_services);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	[Fact]
	public void ImplementIDispatchBuilder()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDispatchBuilder>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(DispatchBuilder).IsPublic.ShouldBeTrue();
		typeof(DispatchBuilder).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DispatchBuilder(null!));
	}

	[Fact]
	public void ExposeServicesProperty()
	{
		// Assert
		_sut.Services.ShouldNotBeNull();
		_sut.Services.ShouldBeSameAs(_services);
	}

	[Fact]
	public void RegisterCoreServicesOnConstruction()
	{
		// Assert - Core services should be registered
		_services.Any(s => s.ServiceType == typeof(PipelineProfileRegistry)).ShouldBeTrue();
	}

	[Fact]
	public void ReturnSelfFromConfigurePipeline()
	{
		// Act
		var result = _sut.ConfigurePipeline("Test", _ => { });

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenConfigurePipelineNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.ConfigurePipeline(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigurePipelineNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.ConfigurePipeline(string.Empty, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigurePipelineNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.ConfigurePipeline("   ", _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigurePipelineConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.ConfigurePipeline("Test", null!));
	}

	[Fact]
	public void ReturnSelfFromRegisterProfile()
	{
		// Arrange
		var profile = A.Fake<IPipelineProfile>();
		A.CallTo(() => profile.Name).Returns("TestProfile");

		// Act
		var result = _sut.RegisterProfile(profile);

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenRegisterProfileIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.RegisterProfile(null!));
	}

	[Fact]
	public void ReturnSelfFromAddBinding()
	{
		// Act
		var result = _sut.AddBinding(_ => { });

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenAddBindingConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.AddBinding(null!));
	}

	[Fact]
	public void ReturnSelfFromUseMiddleware()
	{
		// Act
		var result = _sut.UseMiddleware<TestMiddleware>();

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void RegisterMiddlewareInServices()
	{
		// Act
		_sut.UseMiddleware<TestMiddleware>();

		// Assert
		_services.Any(s => s.ServiceType == typeof(TestMiddleware)).ShouldBeTrue();
		_services.Any(s => s.ServiceType == typeof(IDispatchMiddleware) &&
						   s.ImplementationType == typeof(TestMiddleware)).ShouldBeTrue();
	}

	[Fact]
	public void ReturnSelfFromConfigureOptions()
	{
		// Act
		var result = _sut.ConfigureOptions<DispatchOptions>(_ => { });

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenConfigureOptionsConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.ConfigureOptions<DispatchOptions>(null!));
	}

	[Fact]
	public void ReturnSelfFromWithPipelineProfiles()
	{
		// Act
		var result = _sut.WithPipelineProfiles(_ => { });

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenWithPipelineProfilesConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.WithPipelineProfiles(null!));
	}

	[Fact]
	public void ReturnSelfFromWithOptions()
	{
		// Act
		var result = _sut.WithOptions(_ => { });

		// Assert
		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void ThrowWhenWithOptionsConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.WithOptions(null!));
	}

	[Fact]
	public void ConfigureOptionsViaWithOptions()
	{
		// Arrange
		var timeoutSet = false;

		// Act
		_sut.WithOptions(opt =>
		{
			opt.DefaultTimeout = TimeSpan.FromSeconds(30);
			timeoutSet = true;
		});

		// Assert
		timeoutSet.ShouldBeTrue();
	}

	[Fact]
	public void AllowMultiplePipelineConfigurations()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			_sut.ConfigurePipeline("Pipeline1", _ => { });
			_sut.ConfigurePipeline("Pipeline2", _ => { });
			_sut.ConfigurePipeline("Pipeline3", _ => { });
		});
	}

	[Fact]
	public void AllowMultipleMiddlewareRegistrations()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			_sut.UseMiddleware<TestMiddleware>();
			_sut.UseMiddleware<AnotherTestMiddleware>();
		});
	}

	[Fact]
	public void DisposeWithoutException()
	{
		// Arrange
		var builder = new DispatchBuilder(new ServiceCollection());

		// Act & Assert
		Should.NotThrow(() => builder.Dispose());
	}

	[Fact]
	public void HandleMultipleDisposeCallsGracefully()
	{
		// Arrange
		var builder = new DispatchBuilder(new ServiceCollection());

		// Act & Assert - Multiple dispose calls should not throw
		Should.NotThrow(() =>
		{
			builder.Dispose();
			builder.Dispose();
			builder.Dispose();
		});
	}

	[Fact]
	public void AllowChainedConfiguration()
	{
		// Arrange
		var profile = A.Fake<IPipelineProfile>();
		A.CallTo(() => profile.Name).Returns("ChainTest");

		// Act & Assert - Chaining should work
		var result = _sut
			.ConfigurePipeline("Test", _ => { })
			.RegisterProfile(profile)
			.UseMiddleware<TestMiddleware>()
			.WithOptions(opt => opt.Features.EnableMetrics = true)
			.AddBinding(_ => { });

		result.ShouldBeSameAs(_sut);
	}

	/// <summary>
	/// Test middleware implementation for testing purposes.
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
	/// Another test middleware implementation for testing purposes.
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
