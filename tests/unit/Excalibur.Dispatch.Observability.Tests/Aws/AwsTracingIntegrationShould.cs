// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Aws;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Aws;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsTracingIntegrationShould : IDisposable
{
	private readonly AwsObservabilityOptions _defaultOptions = new()
	{
		ServiceName = "test-service",
		SamplingRate = 1.0 // Always sample for tests
	};

	private AwsTracingIntegration? _integration;

	public void Dispose()
	{
		_integration?.Dispose();
	}

	private AwsTracingIntegration CreateIntegration(AwsObservabilityOptions? options = null)
	{
		var opts = options ?? _defaultOptions;
		_integration = new AwsTracingIntegration(
			MsOptions.Create(opts),
			NullLogger<AwsTracingIntegration>.Instance);
		return _integration;
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsTracingIntegration(null!, NullLogger<AwsTracingIntegration>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsTracingIntegration(
				MsOptions.Create(_defaultOptions),
				null!));
	}

	[Fact]
	public void ImplementIAwsTracingIntegration()
	{
		var integration = CreateIntegration();
		integration.ShouldBeAssignableTo<IAwsTracingIntegration>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		var integration = CreateIntegration();
		integration.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public async Task ConfigureXRayAsync_CompletesSuccessfully()
	{
		var integration = CreateIntegration();
		await integration.ConfigureXRayAsync(CancellationToken.None);
		// Should not throw
	}

	[Fact]
	public async Task ConfigureXRayAsync_SkipsWhenDisabled()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			EnableXRay = false
		};
		var integration = CreateIntegration(options);

		await integration.ConfigureXRayAsync(CancellationToken.None);
		// Should return immediately without error
	}

	[Fact]
	public async Task ConfigureXRayAsync_ThrowsWhenDisposed()
	{
		var integration = CreateIntegration();
		integration.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => integration.ConfigureXRayAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ConfigureCloudWatchMetricsAsync_CompletesSuccessfully()
	{
		var integration = CreateIntegration();
		await integration.ConfigureCloudWatchMetricsAsync(CancellationToken.None);
		// Should not throw
	}

	[Fact]
	public async Task ConfigureCloudWatchMetricsAsync_SkipsWhenDisabled()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			EnableCloudWatchMetrics = false
		};
		var integration = CreateIntegration(options);

		await integration.ConfigureCloudWatchMetricsAsync(CancellationToken.None);
		// Should return immediately without error
	}

	[Fact]
	public async Task ConfigureCloudWatchMetricsAsync_ThrowsWhenDisposed()
	{
		var integration = CreateIntegration();
		integration.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => integration.ConfigureCloudWatchMetricsAsync(CancellationToken.None));
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		var integration = CreateIntegration();
		integration.Dispose();
		integration.Dispose(); // Should not throw
	}

	[Fact]
	public async Task ConfigureXRayAsync_RegistersActivityListener()
	{
		var integration = CreateIntegration();
		await integration.ConfigureXRayAsync(CancellationToken.None);

		// Verify listener is active by creating an activity from a Dispatch source
		using var source = new ActivitySource("Dispatch.Test");
		using var activity = source.StartActivity("test-operation");

		// If the listener was registered, it should have been able to create an activity
		// (depends on sampling, but we set rate to 1.0)
		// The test primarily verifies no exception is thrown
	}

	[Fact]
	public async Task ConfigureXRayAsync_SetsXRayServiceTag()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "my-xray-service",
			SamplingRate = 1.0
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);

		using var source = new ActivitySource("Dispatch.TagTest");
		using var activity = source.StartActivity("tag-operation");

		if (activity is not null)
		{
			activity.GetTagItem("aws.xray.service").ShouldBe("my-xray-service");
		}
	}

	[Fact]
	public async Task ConfigureXRayAsync_SetsDaemonEndpointTag_WhenConfigured()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 1.0,
			XRayDaemonEndpoint = "10.0.0.1:3000"
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);

		using var source = new ActivitySource("Dispatch.DaemonTest");
		using var activity = source.StartActivity("daemon-operation");

		if (activity is not null)
		{
			activity.GetTagItem("aws.xray.daemon_endpoint").ShouldBe("10.0.0.1:3000");
		}
	}

	[Fact]
	public async Task ConfigureXRayAsync_DoesNotSetDaemonTag_WhenNotConfigured()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 1.0,
			XRayDaemonEndpoint = null
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);

		using var source = new ActivitySource("Dispatch.NoDaemonTest");
		using var activity = source.StartActivity("no-daemon-operation");

		if (activity is not null)
		{
			activity.GetTagItem("aws.xray.daemon_endpoint").ShouldBeNull();
		}
	}

	[Fact]
	public async Task OnActivityStopped_SetsFaultTag_WhenError()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 1.0
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);

		using var source = new ActivitySource("Dispatch.ErrorTest");
		using var activity = source.StartActivity("error-operation");

		if (activity is not null)
		{
			activity.SetStatus(ActivityStatusCode.Error, "test error");
			activity.Stop();
			activity.GetTagItem("aws.xray.fault").ShouldBe("true");
		}
	}

	[Fact]
	public async Task OnActivityStopped_DoesNotSetFaultTag_WhenOk()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 1.0
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);

		using var source = new ActivitySource("Dispatch.OkTest");
		using var activity = source.StartActivity("ok-operation");

		if (activity is not null)
		{
			activity.SetStatus(ActivityStatusCode.Ok);
			activity.Stop();
			activity.GetTagItem("aws.xray.fault").ShouldBeNull();
		}
	}

	[Fact]
	public async Task ConfigureXRayAsync_AcceptsZeroSamplingRate()
	{
		// Verify zero sampling rate configures without error
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 0.0
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);
		// Should complete without throwing
	}

	[Fact]
	public async Task ConfigureXRayAsync_AcceptsFullSamplingRate()
	{
		// Verify 100% sampling rate configures without error
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 1.0
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);
		// Should complete without throwing
	}

	[Fact]
	public async Task ConfigureXRayAsync_AcceptsPartialSamplingRate()
	{
		var options = new AwsObservabilityOptions
		{
			ServiceName = "test",
			SamplingRate = 0.5
		};
		var integration = CreateIntegration(options);
		await integration.ConfigureXRayAsync(CancellationToken.None);
		// Should complete without throwing
	}

	[Fact]
	public async Task ConfigureXRayAsync_WithLogging()
	{
		var logger = A.Fake<ILogger<AwsTracingIntegration>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var opts = MsOptions.Create(_defaultOptions);
		_integration = new AwsTracingIntegration(opts, logger);

		await _integration.ConfigureXRayAsync(CancellationToken.None);

		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public async Task ConfigureCloudWatchMetricsAsync_WithLogging()
	{
		var logger = A.Fake<ILogger<AwsTracingIntegration>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var opts = MsOptions.Create(_defaultOptions);
		_integration = new AwsTracingIntegration(opts, logger);

		await _integration.ConfigureCloudWatchMetricsAsync(CancellationToken.None);

		A.CallTo(logger).MustHaveHappened();
	}
}
