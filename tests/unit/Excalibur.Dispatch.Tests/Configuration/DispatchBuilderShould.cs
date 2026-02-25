// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchBuilderShould : IDisposable
{
	private readonly ServiceCollection _services = new();
	private DispatchBuilder? _builder;

	public void Dispose()
	{
		_builder?.Dispose();
	}

	[Fact]
	public void InitializeWithServiceCollection()
	{
		// Act
		_builder = new DispatchBuilder(_services);

		// Assert
		_builder.Services.ShouldBe(_services);
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DispatchBuilder(null!));
	}

	[Fact]
	public void ConfigurePipelineReturnsSelf()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.ConfigurePipeline("Default", _ => { });

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void ConfigurePipelineThrowsForNullName()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentException>(() => _builder.ConfigurePipeline(null!, _ => { }));
		Should.Throw<ArgumentException>(() => _builder.ConfigurePipeline("", _ => { }));
		Should.Throw<ArgumentException>(() => _builder.ConfigurePipeline("   ", _ => { }));
	}

	[Fact]
	public void ConfigurePipelineThrowsForNullAction()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.ConfigurePipeline("Default", null!));
	}

	[Fact]
	public void RegisterProfileReturnsSelf()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);
		var profile = A.Fake<IPipelineProfile>();

		// Act
		var result = _builder.RegisterProfile(profile);

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void RegisterProfileThrowsForNull()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.RegisterProfile(null!));
	}

	[Fact]
	public void AddBindingReturnsSelf()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.AddBinding(_ => { });

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void AddBindingThrowsForNull()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.AddBinding(null!));
	}

	[Fact]
	public void UseMiddlewareRegistersMiddlewareType()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.UseMiddleware<TestMiddleware>();

		// Assert
		result.ShouldBeSameAs(_builder);
		_services.Any(d => d.ServiceType == typeof(TestMiddleware)).ShouldBeTrue();
	}

	[Fact]
	public void ConfigureOptionsForDispatchOptions()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.ConfigureOptions<DispatchOptions>(opt =>
		{
			opt.MaxConcurrency = 42;
		});

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void ConfigureOptionsThrowsForNullAction()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.ConfigureOptions<DispatchOptions>(null!));
	}

	[Fact]
	public void WithOptionsConfiguresDispatchOptions()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.WithOptions(opt =>
		{
			opt.MaxConcurrency = 10;
			opt.UseLightMode = true;
		});

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void WithOptionsThrowsForNull()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.WithOptions(null!));
	}

	[Fact]
	public void WithPipelineProfilesConfiguresProfiles()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act
		var result = _builder.WithPipelineProfiles(_ => { });

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void WithPipelineProfilesThrowsForNull()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _builder.WithPipelineProfiles(null!));
	}

	[Fact]
	public void DisposeIsIdempotent()
	{
		// Arrange
		_builder = new DispatchBuilder(_services);

		// Act & Assert — should not throw
		_builder.Dispose();
		_builder.Dispose();
	}

	private sealed class TestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken) =>
			next(message, context, cancellationToken);
	}
}
