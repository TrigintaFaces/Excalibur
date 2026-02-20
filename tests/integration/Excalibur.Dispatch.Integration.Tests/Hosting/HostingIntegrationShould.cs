// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Integration.Tests.Hosting;

/// <summary>
/// Integration tests for ASP.NET Core and generic host integration.
/// </summary>
public sealed class HostingIntegrationShould : IntegrationTestBase
{
	#region Host Builder Tests

	[Fact]
	public async Task Host_StartsAndStops()
	{
		// Arrange
		var started = false;
		var stopped = false;

		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddHostedService(sp =>
					new TestHostedService(
						() => started = true,
						() => stopped = true));
			})
			.Build();

		// Act
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		started.ShouldBeTrue();
		stopped.ShouldBeTrue();
	}

	[Fact]
	public async Task Host_ResolvesRegisteredServices()
	{
		// Arrange
		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddSingleton<ITestService, TestService>();
			})
			.Build();

		// Act
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);
		var service = host.Services.GetService<ITestService>();
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public async Task Host_ConfiguresLogging()
	{
		// Arrange
		var logMessages = new List<string>();

		var host = Host.CreateDefaultBuilder()
			.ConfigureLogging(logging =>
			{
				_ = logging.ClearProviders();
				_ = logging.AddProvider(new TestLoggerProvider(logMessages));
			})
			.Build();

		// Act
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);
		var logger = host.Services.GetRequiredService<ILogger<HostingIntegrationShould>>();
		logger.LogInformation("Test log message");
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		logMessages.ShouldContain(msg => msg.Contains("Test log message"));
	}

	#endregion

	#region Configuration Tests

	[Fact]
	public async Task Host_LoadsConfiguration()
	{
		// Arrange
		var host = Host.CreateDefaultBuilder()
			.ConfigureAppConfiguration((context, config) =>
			{
				_ = config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["TestSetting:Value"] = "configured-value"
				});
			})
			.Build();

		// Act
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);
		var config = host.Services.GetRequiredService<IConfiguration>();
		var value = config["TestSetting:Value"];
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		value.ShouldBe("configured-value");
	}

	#endregion

	#region Multiple Hosted Services Tests

	[Fact]
	public async Task Host_StartsMultipleHostedServices()
	{
		// Arrange
		var startedServices = new List<string>();

		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddHostedService(sp =>
					new NamedHostedService1("Service1", startedServices));
				_ = services.AddHostedService(sp =>
					new NamedHostedService2("Service2", startedServices));
			})
			.Build();

		// Act
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		startedServices.ShouldContain("Service1");
		startedServices.ShouldContain("Service2");
	}

	#endregion

	#region Test Helpers

	private interface ITestService
	{
		string GetValue();
	}

	private sealed class TestService : ITestService
	{
		public string GetValue() => "test-value";
	}

	private sealed class TestHostedService(Action? onStart = null, Action? onStop = null) : IHostedService
	{
		public Task StartAsync(CancellationToken cancellationToken)
		{
			onStart?.Invoke();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			onStop?.Invoke();
			return Task.CompletedTask;
		}
	}

	private sealed class NamedHostedService1(string name, List<string> startedServices) : IHostedService
	{
		public Task StartAsync(CancellationToken cancellationToken)
		{
			startedServices.Add(name);
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class NamedHostedService2(string name, List<string> startedServices) : IHostedService
	{
		public Task StartAsync(CancellationToken cancellationToken)
		{
			startedServices.Add(name);
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class TestLoggerProvider(List<string> logMessages) : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName) => new TestLogger(logMessages);

		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}

	private sealed class TestLogger(List<string> logMessages) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			logMessages.Add(formatter(state, exception));
		}
	}

	#endregion
}
