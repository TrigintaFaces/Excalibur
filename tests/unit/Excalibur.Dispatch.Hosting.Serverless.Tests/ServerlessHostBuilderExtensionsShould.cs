// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessHostBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessHostBuilderExtensionsShould : UnitTestBase
{
	#region UseServerlessHosting Tests

	[Fact]
	public void UseServerlessHosting_WithNullHostBuilder_ThrowsArgumentNullException()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => hostBuilder.UseServerlessHosting());
	}

	[Fact]
	public void UseServerlessHosting_WithoutOptions_ReturnsHostBuilder()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Returns(hostBuilder);

		// Act
		var result = hostBuilder.UseServerlessHosting();

		// Assert
		result.ShouldBe(hostBuilder);
	}

	[Fact]
	public void UseServerlessHosting_WithOptions_ConfiguresOptions()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Returns(hostBuilder);
		var optionsConfigured = false;

		// Act
		var result = hostBuilder.UseServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
			optionsConfigured = true;
		});

		// Assert
		result.ShouldBe(hostBuilder);
		optionsConfigured.ShouldBeTrue();
	}

	#endregion

	#region UseAwsLambda Tests

	[Fact]
	public void UseAwsLambda_WithNullHostBuilder_ThrowsArgumentNullException()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => hostBuilder.UseAwsLambda());
	}

	[Fact]
	public void UseAwsLambda_WithoutOptions_ReturnsHostBuilder()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Returns(hostBuilder);

		// Act
		var result = hostBuilder.UseAwsLambda();

		// Assert
		result.ShouldBe(hostBuilder);
	}
	#endregion

	#region UseAzureFunctions Tests

	[Fact]
	public void UseAzureFunctions_WithNullHostBuilder_ThrowsArgumentNullException()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => hostBuilder.UseAzureFunctions());
	}

	[Fact]
	public void UseAzureFunctions_WithoutOptions_ReturnsHostBuilder()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Returns(hostBuilder);

		// Act
		var result = hostBuilder.UseAzureFunctions();

		// Assert
		result.ShouldBe(hostBuilder);
	}
	#endregion

	#region UseGoogleCloudFunctions Tests

	[Fact]
	public void UseGoogleCloudFunctions_WithNullHostBuilder_ThrowsArgumentNullException()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => hostBuilder.UseGoogleCloudFunctions());
	}

	[Fact]
	public void UseGoogleCloudFunctions_WithoutOptions_ReturnsHostBuilder()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Returns(hostBuilder);

		// Act
		var result = hostBuilder.UseGoogleCloudFunctions();

		// Assert
		result.ShouldBe(hostBuilder);
	}
	#endregion
}