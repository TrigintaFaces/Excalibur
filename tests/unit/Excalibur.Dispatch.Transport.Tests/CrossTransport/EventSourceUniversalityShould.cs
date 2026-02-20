// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Verifies <see cref="ITransportSubscriber"/> interface shape and <see cref="MessageAction"/> enum.
/// <para>
/// Sprint 528: <c>IEventSource</c> removed (dead code — used dispatch-level types).
/// Replaced by <see cref="ITransportSubscriber"/> which works at the transport level
/// with <see cref="TransportReceivedMessage"/> and <see cref="MessageAction"/> (ADR-116).
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSubscriberInterfaceShould
{
	[Fact]
	public void ITransportSubscriber_HasSubscribeAsyncMethod()
	{
		var methods = typeof(ITransportSubscriber).GetMethods();
		var subscribeMethod = methods.SingleOrDefault(m => m.Name == "SubscribeAsync");

		subscribeMethod.ShouldNotBeNull("ITransportSubscriber should have a SubscribeAsync method");
		subscribeMethod.ReturnType.ShouldBe(typeof(Task));

		var parameters = subscribeMethod.GetParameters();
		parameters.Length.ShouldBe(2, "SubscribeAsync should have exactly 2 parameters");
		parameters[0].ParameterType.ShouldBe(
			typeof(Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void ITransportSubscriber_HasSourceProperty()
	{
		var sourceProperty = typeof(ITransportSubscriber).GetProperty("Source");
		sourceProperty.ShouldNotBeNull("ITransportSubscriber should have a Source property");
		sourceProperty.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void ITransportSubscriber_ExtendsIAsyncDisposable()
	{
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(ITransportSubscriber))
			.ShouldBeTrue("ITransportSubscriber should extend IAsyncDisposable");
	}

	[Fact]
	public void MessageAction_HasExpectedValues()
	{
		Enum.GetValues<MessageAction>().Length.ShouldBe(3);
		Enum.IsDefined(MessageAction.Acknowledge).ShouldBeTrue();
		Enum.IsDefined(MessageAction.Reject).ShouldBeTrue();
		Enum.IsDefined(MessageAction.Requeue).ShouldBeTrue();
	}

	[Fact]
	public void ITransportReceiver_IsPullBased_ITransportSubscriber_IsPushBased()
	{
		// ITransportReceiver: pull-based — caller calls ReceiveAsync to get messages
		var receiverMethods = typeof(ITransportReceiver).GetMethods()
			.Where(m => m.DeclaringType == typeof(ITransportReceiver))
			.Select(m => m.Name)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToArray();

		receiverMethods.ShouldContain("ReceiveAsync");
		receiverMethods.ShouldContain("AcknowledgeAsync");
		receiverMethods.ShouldContain("RejectAsync");

		// ITransportSubscriber: push-based — transport pushes messages to handler callback
		var subscriberMethods = typeof(ITransportSubscriber).GetMethods()
			.Where(m => m.DeclaringType == typeof(ITransportSubscriber))
			.Select(m => m.Name)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToArray();

		subscriberMethods.ShouldContain("SubscribeAsync");
	}
}
