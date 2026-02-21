// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsDispatchBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class MetricsDispatchBuilderExtensionsShould : UnitTestBase
{
	#region AddDispatchMetricsInstrumentation Tests

	[Fact]
	public void AddDispatchMetricsInstrumentation_ThrowOnNullBuilder()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddDispatchMetricsInstrumentation());
	}

	[Fact]
	public void AddDispatchMetricsInstrumentation_ReturnBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddDispatchMetricsInstrumentation();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddDispatchMetricsInstrumentation_RegisterDispatchMetrics()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.AddDispatchMetricsInstrumentation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DispatchMetrics));
	}

	#endregion

	#region WithMetricsOptions (Action) Tests

	[Fact]
	public void WithMetricsOptions_Action_ThrowOnNullBuilder()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions(_ => { }));
	}

	[Fact]
	public void WithMetricsOptions_Action_ThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions((Action<ObservabilityOptions>)null!));
	}

	[Fact]
	public void WithMetricsOptions_Action_ReturnBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.WithMetricsOptions(_ => { });

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void WithMetricsOptions_Action_ApplyConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		// Act
		_ = builder.WithMetricsOptions(_ => { });
		var provider = services.BuildServiceProvider();

		// Since we used Configure<T>, we can check if Options<T> is registered
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>();

		// Assert
		options.ShouldNotBeNull();
	}

	#endregion

	#region WithMetricsOptions (IConfiguration) Tests

	[Fact]
	public void WithMetricsOptions_Configuration_ThrowOnNullBuilder()
	{
		// Arrange
		IDispatchBuilder builder = null!;
		var configuration = A.Fake<IConfiguration>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions(configuration));
	}

	[Fact]
	public void WithMetricsOptions_Configuration_ThrowOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions((IConfiguration)null!));
	}

	[Fact]
	public void WithMetricsOptions_Configuration_ReturnBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var configuration = A.Fake<IConfiguration>();

		// Act
		var result = builder.WithMetricsOptions(configuration);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion
}
