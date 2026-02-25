// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

namespace Excalibur.Hosting.Tests.AzureFunctions;

/// <summary>
/// Unit tests for <see cref="AzureFunctionsHostProvider" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class AzureFunctionsHostProviderShould : UnitTestBase
{
	private readonly AzureFunctionsHostProvider _sut;
	private readonly ILogger<AzureFunctionsHostProvider> _logger;

	public AzureFunctionsHostProviderShould()
	{
		_logger = NullLogger<AzureFunctionsHostProvider>.Instance;
		_sut = new AzureFunctionsHostProvider(_logger);
	}

	[Fact]
	public void Platform_ReturnsAzureFunctions()
	{
		// Act
		var result = _sut.Platform;

		// Assert
		result.ShouldBe(ServerlessPlatform.AzureFunctions);
	}

	[Fact]
	public void IsAvailable_ReturnsFalse_WhenAzureFunctionsSupportNotEnabled()
	{
		// Act - Without AZURE_FUNCTIONS_SUPPORT defined, IsAvailable always returns false
		var result = _sut.IsAvailable;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ConfigureServices_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ServerlessHostOptions();

		// Act & Assert
		Should.NotThrow(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureServices_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = new ServerlessHostOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureServices_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		ServerlessHostOptions options = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureHost_DoesNotThrow()
	{
		// Arrange
		var hostBuilder = Host.CreateDefaultBuilder();
		var options = new ServerlessHostOptions();

		// Act & Assert
		Should.NotThrow(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void ConfigureHost_ThrowsOnNullHostBuilder()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;
		var options = new ServerlessHostOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void ConfigureHost_ThrowsOnNullOptions()
	{
		// Arrange
		var hostBuilder = Host.CreateDefaultBuilder();
		ServerlessHostOptions options = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void CreateContext_ThrowsForInvalidContext()
	{
		// Arrange
		var invalidContext = new object();

		// Act & Assert - Without AZURE_FUNCTIONS_SUPPORT, all contexts are invalid
		_ = Should.Throw<ArgumentException>(() => _sut.CreateContext(invalidContext));
	}

	[Fact]
	public void CreateContext_ThrowsOnNullContext()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.CreateContext(null!));
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsOnNullInput()
	{
		// Arrange
		string? input = null;
		var mockContext = new MockServerlessContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync(
				input!,
				mockContext,
				(i, c, ct) => Task.FromResult(i),
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsOnNullContext()
	{
		// Arrange
		var input = "test";
		IServerlessContext context = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync(
				input,
				context,
				(i, c, ct) => Task.FromResult(i),
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsOnNullHandler()
	{
		// Arrange
		var input = "test";
		var mockContext = new MockServerlessContext();
		Func<string, IServerlessContext, CancellationToken, Task<string>> handler = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExecuteAsync(input, mockContext, handler, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ExecutesHandler()
	{
		// Arrange
		var input = "test-input";
		var mockContext = new MockServerlessContext();
		var executed = false;

		// Act
		var result = await _sut.ExecuteAsync(
			input,
			mockContext,
			(i, c, ct) =>
			{
				executed = true;
				return Task.FromResult($"processed-{i}");
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
		result.ShouldBe("processed-test-input");
	}

	private sealed class MockServerlessContext : IServerlessContext
	{
		public object PlatformContext => new object();
		public ServerlessPlatform Platform => ServerlessPlatform.AzureFunctions;
		public string RequestId => "mock-request-id";
		public string FunctionName => "MockFunction";
		public string FunctionVersion => "1.0.0";
		public string InvokedFunctionArn => "mock-arn";
		public int MemoryLimitInMB => 128;
		public TimeSpan RemainingTime => TimeSpan.FromMinutes(5);
		public TimeSpan ElapsedTime => TimeSpan.Zero;
		public string LogGroupName => "mock-log-group";
		public string LogStreamName => "mock-log-stream";
		public string CloudProvider => "Azure";
		public string Region => "eastus";
		public string AccountId => "mock-account";
		public DateTimeOffset ExecutionDeadline => DateTimeOffset.UtcNow.AddMinutes(5);
		public ILogger Logger => NullLogger.Instance;
		public IDictionary<string, object> Properties => new Dictionary<string, object>();
		public object? GetService(Type serviceType) => null;
		public TraceContext? TraceContext => null;
		public void Dispose() { }
	}
}
