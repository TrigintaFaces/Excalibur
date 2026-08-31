// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Security;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Regression tests for the RabbitMQ transport wired-path option gaps (o0wv4k):
/// the configured QoS prefetch default and the connection-recovery options must be
/// honored instead of being hardcoded / disabled.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqTransportOptionWiringShould : UnitTestBase
{
	[Fact]
	public async Task ApplyDefaultPrefetch_WhenNoQueuePrefetchIsConfigured()
	{
		// Arrange -- no ConfigureQueue, so the subscriber falls back to the documented default (100).
		var channel = A.Fake<IChannel>();
		var services = new ServiceCollection();
		services.AddLogging();

		// Non-default credentials: options validation is now eager at resolution (ValidateOnStart wired
		// with the ingress payload-guard option), so the pre-existing default-credentials (guest:guest)
		// guard fires at GetRequiredKeyedService. Secure creds keep this test on its actual subject (QoS).
		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqp://appuser:S3cretPw0rd@localhost:5672/");
		});

		// Override the transport's real IChannel with a fake (registered last so it wins resolution),
		// so the subscriber applies QoS against the fake instead of opening a live broker connection.
		services.AddSingleton(channel);

		await using var provider = services.BuildServiceProvider();
		var subscriber = provider.GetRequiredKeyedService<ITransportSubscriber>("test");

		// Act -- a pre-cancelled token lets SubscribeAsync apply QoS then unwind immediately.
		// The unwind surfaces as OperationCanceledException, which is expected and swallowed; the
		// behavior under test is that BasicQosAsync was invoked with the resolved default prefetch.
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		try
		{
			await subscriber.SubscribeAsync(
				(_, _) => Task.FromResult(MessageAction.Acknowledge),
				cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected: the pre-cancelled token unwinds the subscribe loop after QoS is applied.
		}

		// Assert -- BasicQosAsync was invoked with the default prefetch of 100, not 0 (disabled).
		A.CallTo(() => channel.BasicQosAsync(
			0u, (ushort)100, false, A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public void HonorConnectionRecoveryOptions_OnTheConnectionFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			// TLS: the transport refuses to build a plaintext connection factory under its shipping
			// posture, so this arm runs under that posture rather than opting out of it. Its subject is
			// recovery, not security.
			_ = rmq.ConnectionString("amqps://guest:guest@localhost:5671/")
				.AutomaticRecovery(enabled: false, networkRecoveryInterval: TimeSpan.FromSeconds(42));
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IConnectionFactory>();

		// Assert -- mapped from options, not the hardcoded true / 10s.
		var rabbitFactory = factory.ShouldBeOfType<ConnectionFactory>();
		rabbitFactory.AutomaticRecoveryEnabled.ShouldBeFalse();
		rabbitFactory.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void WaiveTrustValidationErrors_WhenAcceptUntrustedCertificatesIsSet()
	{
		// Arrange -- amqps carries TLS without entering the UseSsl branch, which is the path the
		// relaxation must also reach.
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqps://appuser:S3cretPw0rd@localhost:5671/")
				.UseSsl(ssl => ssl.AcceptUntrustedCertificates = true);
		});

		// Act -- AcceptablePolicyErrors is the value the TLS handshake consults, so asserting it is
		// asserting the handshake outcome, not that the property round-trips.
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IConnectionFactory>().ShouldBeOfType<ConnectionFactory>();

		// Assert -- the two errors that describe an untrusted certificate are waived...
		factory.Ssl.AcceptablePolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
		factory.Ssl.AcceptablePolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch).ShouldBeTrue();

		// ...and an absent certificate is NOT, so the connection can never become unauthenticated
		// without the broker presenting something.
		factory.Ssl.AcceptablePolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable).ShouldBeFalse();
	}

	[Fact]
	public void RequireAValidatingCertificate_WhenAcceptUntrustedCertificatesIsNotSet()
	{
		// Arrange -- same registration, option left at its default.
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqps://appuser:S3cretPw0rd@localhost:5671/");
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IConnectionFactory>().ShouldBeOfType<ConnectionFactory>();

		// Assert -- nothing is waived: full validation, including the hostname check.
		//
		// This arm is load-bearing beyond "the option is off". The client library's Uri setter waives
		// RemoteCertificateNameMismatch by itself when the scheme is amqps, so before the transport set
		// this posture the two documented ways of enabling TLS authenticated the broker differently:
		// amqps skipped the hostname check, UseSsl did not. Asserting None here is what holds the two
		// paths to one posture.
		factory.Ssl.AcceptablePolicyErrors.ShouldBe(SslPolicyErrors.None);
	}

	[Fact]
	public void ApplyTheSameCertificatePosture_OnTheUseSslPathAsOnAmqps()
	{
		// Arrange -- TLS via UseSsl rather than an amqps scheme. The posture must not depend on which
		// of the two the consumer picked.
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqp://appuser:S3cretPw0rd@localhost:5672/")
				.UseSsl(ssl => ssl.AcceptUntrustedCertificates = true);
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IConnectionFactory>().ShouldBeOfType<ConnectionFactory>();

		// Assert
		factory.Ssl.AcceptablePolicyErrors.ShouldBe(
			SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);
	}

	[Fact]
	public void ThrowWhenAutomaticRecoveryIntervalIsZero()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenAutomaticRecoveryIntervalIsNegative()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.FromSeconds(-1)));
	}
}
