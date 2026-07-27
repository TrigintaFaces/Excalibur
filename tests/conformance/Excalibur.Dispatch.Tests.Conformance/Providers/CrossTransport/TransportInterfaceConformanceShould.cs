// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// Verifies the transports implement the core transport interfaces.
/// Uses reflection to scan each transport assembly for implementations of:
/// <see cref="ITransportSender"/>, <see cref="ITransportReceiver"/>,
/// <see cref="IDeadLetterQueueManager"/>.
/// <para>
/// <see cref="ITransportSender"/> / <see cref="ITransportReceiver"/> conformance applies to all
/// 5 transports. <see cref="IDeadLetterQueueManager"/> is a MANAGED-DLQ concern implemented by the
/// 4 managed transports (Azure SB, Google PubSub, Kafka, RabbitMQ). AWS SQS deliberately has NO
/// <see cref="IDeadLetterQueueManager"/> implementation — it uses the native SQS redrive-policy
/// (maxReceiveCount → SQS DLQ) instead of a managed DLQ manager.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class TransportInterfaceConformanceShould
{
	/// <summary>
	/// Transport assembly anchors — one public type from each transport assembly.
	/// </summary>
	private static readonly (string Name, Assembly Assembly)[] s_transports =
	[
		("AzureServiceBus", typeof(AzureServiceBusOptions).Assembly),
		("AwsSqs", typeof(AwsSqsOptions).Assembly),
		("GooglePubSub", typeof(GooglePubSubOptions).Assembly),
		("Kafka", typeof(KafkaOptions).Assembly),
		("RabbitMQ", typeof(RabbitMqOptions).Assembly),
	];

	#region Interface Conformance (ITransportSender / ITransportReceiver)

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_ITransportSender_Implementation(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var implementations = FindImplementations(assembly, typeof(ITransportSender));
		implementations.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have at least one ITransportSender implementation");
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_ITransportReceiver_Implementation(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var implementations = FindImplementations(assembly, typeof(ITransportReceiver));
		implementations.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have at least one ITransportReceiver implementation");
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_IAsyncDisposable_On_TransportSender(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var senders = FindImplementations(assembly, typeof(ITransportSender));
		senders.ShouldNotBeEmpty($"{transportName} should have transport senders");

		foreach (var sender in senders)
		{
			typeof(IAsyncDisposable).IsAssignableFrom(sender).ShouldBeTrue(
				$"{transportName} transport sender {sender.Name} MUST implement IAsyncDisposable");
		}
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_IAsyncDisposable_On_TransportReceiver(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var receivers = FindImplementations(assembly, typeof(ITransportReceiver));
		receivers.ShouldNotBeEmpty($"{transportName} should have transport receivers");

		foreach (var receiver in receivers)
		{
			typeof(IAsyncDisposable).IsAssignableFrom(receiver).ShouldBeTrue(
				$"{transportName} transport receiver {receiver.Name} MUST implement IAsyncDisposable");
		}
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_GetService_On_TransportSender(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var senders = FindImplementations(assembly, typeof(ITransportSender));
		senders.ShouldNotBeEmpty($"{transportName} should have transport senders");

		foreach (var sender in senders)
		{
			var getServiceMethod = sender.GetMethod("GetService", [typeof(Type)]);
			getServiceMethod.ShouldNotBeNull(
				$"{transportName} transport sender {sender.Name} MUST have GetService(Type) method");
		}
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_GetService_On_TransportReceiver(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var receivers = FindImplementations(assembly, typeof(ITransportReceiver));
		receivers.ShouldNotBeEmpty($"{transportName} should have transport receivers");

		foreach (var receiver in receivers)
		{
			var getServiceMethod = receiver.GetMethod("GetService", [typeof(Type)]);
			getServiceMethod.ShouldNotBeNull(
				$"{transportName} transport receiver {receiver.Name} MUST have GetService(Type) method");
		}
	}

	#endregion Interface Conformance (ITransportSender / ITransportReceiver)

	#region Common Interface Conformance

	[Theory]
	[MemberData(nameof(ManagedDlqTransportNames))]
	public void Have_IDeadLetterQueueManager_Implementation(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var implementations = FindImplementations(assembly, typeof(IDeadLetterQueueManager));
		implementations.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have at least one IDeadLetterQueueManager implementation");
	}

	[Fact]
	public void AwsSqs_HasNoIDeadLetterQueueManager_ByDesign()
	{
		var assembly = GetAssembly("AwsSqs");
		var implementations = FindImplementations(assembly, typeof(IDeadLetterQueueManager));
		implementations.Length.ShouldBe(
			0,
			"AWS SQS MUST have NO IDeadLetterQueueManager implementation by design — it uses the native " +
			"SQS redrive-policy (maxReceiveCount → SQS DLQ), not a managed DLQ manager. Found: " +
			string.Join(", ", implementations.Select(t => t.FullName)));
	}

	#endregion Common Interface Conformance

	public static TheoryData<string> TransportNames()
	{
		var data = new TheoryData<string>();
		foreach (var (name, _) in s_transports)
		{
			data.Add(name);
		}

		return data;
	}

	/// <summary>
	/// The 4 MANAGED transports that implement <see cref="IDeadLetterQueueManager"/>.
	/// Excludes AWS SQS, which uses the native SQS redrive-policy instead of a managed DLQ manager.
	/// </summary>
	public static TheoryData<string> ManagedDlqTransportNames()
	{
		var data = new TheoryData<string>();
		foreach (var (name, _) in s_transports.Where(t => t.Name != "AwsSqs"))
		{
			data.Add(name);
		}

		return data;
	}

	private static Assembly GetAssembly(string transportName) =>
		s_transports.First(t => t.Name == transportName).Assembly;

	private static Type[] FindImplementations(Assembly assembly, Type interfaceType) =>
		assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false } && interfaceType.IsAssignableFrom(t))
			.ToArray();
}
