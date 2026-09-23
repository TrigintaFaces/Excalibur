// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// tmd249 sub-item 3 — the sender's dedicated publisher-confirms channel
/// (<c>"{name}:sender-confirms"</c>) must be registered via <c>TryAddKeyedSingleton</c>, matching the
/// <c>ITransportSender</c>/<c>ITransportReceiver</c> registrations right beside it (both already
/// <c>TryAdd*</c>, per this method's own doc comment: "lets a consumer override the registration
/// (Microsoft-first)") — not the plain <c>AddKeyedSingleton</c> it used, which would silently discard a
/// consumer's own pre-registered channel for that key.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqConfirmChannelTryAddShould : UnitTestBase
{
	private const string TransportName = "test";
	private static string ConfirmChannelKey => $"{TransportName}:sender-confirms";

	/// <summary>
	/// SAFETY — a consumer registering their own confirm channel BEFORE calling
	/// <c>AddRabbitMQTransport</c> still resolves theirs; the framework's registration must not overwrite it.
	/// </summary>
	[Fact]
	public void ResolveTheConsumersOwnChannel_WhenRegisteredBeforeAddRabbitMQTransport()
	{
		var consumerChannel = A.Fake<IChannel>();
		var services = new ServiceCollection();
		services.AddLogging();

		// Consumer registers their own confirm channel FIRST, under the exact key the transport uses.
		services.AddKeyedSingleton(ConfirmChannelKey, consumerChannel);

		_ = services.AddRabbitMQTransport(TransportName, rmq =>
		{
			_ = rmq.ConnectionString("amqp://appuser:S3cretPw0rd@localhost:5672/");
		});

		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredKeyedService<IChannel>(ConfirmChannelKey);

		resolved.ShouldBeSameAs(
			consumerChannel,
			"TryAdd* must let a consumer-supplied confirm channel win — an unconditional Add would have "
			+ "appended the framework's own factory registration after it, and DI resolves the LAST "
			+ "registration for a given key, silently discarding the consumer's channel.");
	}

	/// <summary>
	/// LIVENESS — with no prior registration, the framework's own confirm-channel registration is the one
	/// present (proves the arm above is not vacuously green because nothing registers under this key at all).
	/// </summary>
	[Fact]
	public void RegisterItsOwnChannel_WhenNoConsumerRegistrationExists()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport(TransportName, rmq =>
		{
			_ = rmq.ConnectionString("amqp://appuser:S3cretPw0rd@localhost:5672/");
		});

		// Assert at the descriptor level, not by resolving: resolving the framework's factory would open a
		// live broker connection (CreateChannelAsync), which this unit test must not require.
		services.ShouldContain(
			d => d.ServiceType == typeof(IChannel) && Equals(d.ServiceKey, ConfirmChannelKey),
			"the framework must still register its own confirm channel under this key when no consumer did.");
	}
}
