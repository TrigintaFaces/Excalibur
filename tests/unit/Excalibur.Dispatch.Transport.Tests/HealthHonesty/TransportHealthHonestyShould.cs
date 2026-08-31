// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;

using Confluent.Kafka;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.HealthHonesty;

/// <summary>
/// A transport must not report health it never established.
/// </summary>
/// <remarks>
/// <para>
/// Each behaviour is locked as a PAIR. The safety arm proves the transport does not claim
/// health over a path that contacted nothing. The liveness arm proves the transport still
/// reports health once it genuinely has it -- without it, "never report healthy" would pass
/// every safety arm here and break every consumer.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportHealthHonestyShould
{
	private static AwsSqsTransportAdapter CreateAdapter()
	{
		var sqsClient = A.Fake<IAmazonSQS>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new AwsSqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
		};

		var bus = new AwsSqsMessageBus(
			sqsClient,
			serializer,
			options,
			NullLogger<AwsSqsMessageBus>.Instance);

		return new AwsSqsTransportAdapter(
			NullLogger<AwsSqsTransportAdapter>.Instance,
			bus,
			A.Fake<IServiceProvider>());
	}

	// ---- SAFETY: a receipt is not written where the work did not happen ----

	[Fact]
	public async Task NotReportHealthyBeforeAnyProbeHasRun()
	{
		await using var adapter = CreateAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		// Nothing has contacted the broker, so nothing established health.
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Unknown);
		metrics.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task NotReportACheckTimestampBeforeAnyProbeHasRun()
	{
		await using var adapter = CreateAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		// A construction-time "now" here reads to an operator as a check that just succeeded.
		metrics.LastCheckTimestamp.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task NotReportHealthyAfterStartAsyncAlone()
	{
		await using var adapter = CreateAdapter();

		await adapter.StartAsync(CancellationToken.None);

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		// StartAsync flips a lifecycle flag; it opens nothing, so it establishes no health.
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Unknown);
	}

	// ---- LIVENESS: the transport still reports what it genuinely established ----

	[Fact]
	public async Task StillReportRunningAfterStartAsync()
	{
		await using var adapter = CreateAdapter();

		await adapter.StartAsync(CancellationToken.None);

		// Lifecycle-started is a claim StartAsync does earn, and consumers gate on it.
		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task ReportHealthyOnceAProbeHasEstablishedIt()
	{
		await using var adapter = CreateAdapter();
		await adapter.StartAsync(CancellationToken.None);

		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		var result = await adapter.CheckHealthAsync(context, CancellationToken.None);
		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Healthy);
		metrics.LastStatus.ShouldNotBe(TransportHealthStatus.Unknown);
		metrics.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task ReportUnhealthyOnceAProbeHasEstablishedThat()
	{
		await using var adapter = CreateAdapter();

		// Never started, so the probe genuinely finds it unhealthy -- an established claim.
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		var result = await adapter.CheckHealthAsync(context, CancellationToken.None);
		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Unhealthy);
	}
}

/// <summary>
/// The Kafka connection flag claims configuration validation is complete, so it must be.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaConnectionValidationShould
{
	// ---- SAFETY ----

	[Fact]
	public async Task NotReportConnectedWhenBootstrapServersAreMissing()
	{
		var config = new ProducerConfig { SecurityProtocol = SecurityProtocol.Ssl };
		await using var transport = new KafkaTransportConnection(
			config,
			new TransportSecurityOptions { RequireTls = false });

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => transport.ConnectAsync(CancellationToken.None));

		transport.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task NotReportConnectedWhenSaslCredentialsAreIncomplete()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9093",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.Plain,
			SaslUsername = "user",
			// SaslPassword deliberately omitted
		};

		await using var transport = new KafkaTransportConnection(
			config,
			new TransportSecurityOptions { RequireTls = false });

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => transport.ConnectAsync(CancellationToken.None));

		transport.IsConnected.ShouldBeFalse();
	}

	// ---- LIVENESS ----

	[Fact]
	public async Task ReportConnectedWhenTheConfigurationIsComplete()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9093",
			SecurityProtocol = SecurityProtocol.Ssl,
		};

		await using var transport = new KafkaTransportConnection(
			config,
			new TransportSecurityOptions { RequireTls = true });

		await transport.ConnectAsync(CancellationToken.None);

		transport.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task ReportConnectedWhenSaslCredentialsAreComplete()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9093",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.Plain,
			SaslUsername = "user",
			SaslPassword = "pass", // pragma: allowlist secret
		};

		await using var transport = new KafkaTransportConnection(
			config,
			new TransportSecurityOptions { RequireTls = true });

		await transport.ConnectAsync(CancellationToken.None);

		transport.IsConnected.ShouldBeTrue();
	}
}
