// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;
using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Tests for Dispatch AzureFunctionsHostProvider.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchAzureFunctionsHostProviderShould : UnitTestBase
{
	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AzureFunctionsHostProvider(null!));
	}

	[Fact]
	public void ConstructSuccessfully()
	{
		// Act
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Assert
		provider.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnAzureFunctionsPlatform()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		provider.Platform.ShouldBe(ServerlessPlatform.AzureFunctions);
	}

	[Fact]
	public void ReturnIsAvailableFalseInTestEnvironment()
	{
		// Arrange — AZURE_FUNCTIONS_ENVIRONMENT not set in test env
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void ReturnSelfForGetServiceWithMatchingType()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act
		var result = provider.GetService(typeof(AzureFunctionsHostProvider));

		// Assert
		result.ShouldBeSameAs(provider);
	}

	[Fact]
	public void ReturnNullForGetServiceWithNonMatchingType()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act
		var result = provider.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => provider.GetService(null!));
	}

	[Fact]
	public void ThrowWhenConfigureServicesServicesIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);
		var options = new ServerlessHostOptions();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => provider.ConfigureServices(null!, options));
	}

	[Fact]
	public void ThrowWhenConfigureServicesOptionsIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => provider.ConfigureServices(services, null!));
	}

	[Fact]
	public void ThrowWhenCreateContextPlatformContextIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => provider.CreateContext(null!));
	}

	[Fact]
	public void ThrowWhenCreateContextPlatformContextIsWrongType()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		Should.Throw<ArgumentException>(() => provider.CreateContext("not-a-function-context"));
	}

	[Fact]
	public async Task ThrowWhenExecuteAsyncInputIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);
		var context = A.Fake<IServerlessContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteAsync<string, string>(null!, context, (_, _, _) => Task.FromResult("ok"), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenExecuteAsyncContextIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteAsync<string, string>("input", null!, (_, _, _) => Task.FromResult("ok"), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenExecuteAsyncHandlerIsNull()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);
		var context = A.Fake<IServerlessContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteAsync<string, string>("input", context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteHandlerSuccessfully()
	{
		// Arrange
		var provider = new AzureFunctionsHostProvider(NullLogger.Instance);
		var context = A.Fake<IServerlessContext>();
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromMinutes(5));

		// Act
		var result = await provider.ExecuteAsync<string, string>(
			"input",
			context,
			(input, _, _) => Task.FromResult($"processed-{input}"),
			CancellationToken.None);

		// Assert
		result.ShouldBe("processed-input");
	}
}
