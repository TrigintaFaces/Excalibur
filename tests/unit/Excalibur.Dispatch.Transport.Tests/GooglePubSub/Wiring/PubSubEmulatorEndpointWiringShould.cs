// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Google.Cloud.PubSub.V1;

using Grpc.Core;
using Grpc.Net.Client;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Wiring;

/// <summary>
/// Locks both default Pub/Sub clients onto the endpoint <c>PUBSUB_EMULATOR_HOST</c> names.
/// </summary>
/// <remarks>
/// The streaming subscriber is built with emulator detection, so a default client built without it
/// sends the two receive paths of one package to two different endpoints off the same environment.
/// The failure is silent — production is a real endpoint, so the consumer sees no error, just a
/// subscription that never delivers what their emulator was handed.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Collection(nameof(PubSubEmulatorEndpointWiringShould))]
[CollectionDefinition(nameof(PubSubEmulatorEndpointWiringShould), DisableParallelization = true)]
public sealed class PubSubEmulatorEndpointWiringShould
{
	private const string EmulatorHostVariable = "PUBSUB_EMULATOR_HOST";

	/// <summary>An address nothing listens on: the channel is never dialled, only inspected.</summary>
	private const string EmulatorHost = "localhost:18085";

	[Fact]
	public void BuildThePullPathDefaultClientAgainstTheEmulator()
	{
		using var _ = EmulatorHostSetTo(EmulatorHost);

		var client = GooglePubSubTransportServiceCollectionExtensions.ResolveSubscriberClient(
			EmptyProvider.Instance, "google-pubsub");

		TargetOf(client.GrpcClient).ShouldBe(EmulatorHost);
	}

	[Fact]
	public void BuildTheSendPathDefaultClientAgainstTheEmulator()
	{
		using var _ = EmulatorHostSetTo(EmulatorHost);

		var client = GooglePubSubTransportServiceCollectionExtensions.ResolvePublisherClient(
			EmptyProvider.Instance, "google-pubsub");

		TargetOf(client.GrpcClient).ShouldBe(EmulatorHost);
	}

	/// <summary>
	/// Reads the endpoint a built client will actually dial.
	/// </summary>
	/// <remarks>
	/// The gRPC channel is not on the client's public surface, so the hops are taken by reflection.
	/// Each hop asserts rather than returning null, so a client library that moves this plumbing makes
	/// the test fail loudly instead of passing on an endpoint it never read.
	/// </remarks>
	private static string TargetOf(ClientBase grpcClient)
	{
		const BindingFlags Any =
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

		var invoker = grpcClient.GetType().GetProperty("CallInvoker", Any)?.GetValue(grpcClient);
		invoker.ShouldNotBeNull("the gRPC client no longer exposes a CallInvoker");

		var inner = invoker.GetType().GetField("invoker", Any)?.GetValue(invoker);
		inner.ShouldNotBeNull("the intercepting call invoker no longer wraps an inner invoker");

		var channel = inner.GetType().GetProperty("Channel", Any)?.GetValue(inner) as GrpcChannel;
		channel.ShouldNotBeNull("the inner call invoker no longer exposes its channel");

		return channel.Target;
	}

	private static EmulatorHostScope EmulatorHostSetTo(string host) => new(host);

	/// <summary>
	/// Sets <c>PUBSUB_EMULATOR_HOST</c> for one test and restores whatever was there before, so the
	/// variable never leaks into a sibling test.
	/// </summary>
	private sealed class EmulatorHostScope : IDisposable
	{
		private readonly string? _prior;

		internal EmulatorHostScope(string host)
		{
			_prior = Environment.GetEnvironmentVariable(EmulatorHostVariable);
			Environment.SetEnvironmentVariable(EmulatorHostVariable, host);
		}

		public void Dispose() => Environment.SetEnvironmentVariable(EmulatorHostVariable, _prior);
	}

	/// <summary>A provider that supplies no client, so the resolvers fall through to their default.</summary>
	private sealed class EmptyProvider : IServiceProvider, IKeyedServiceProvider
	{
		internal static readonly EmptyProvider Instance = new();

		public object? GetService(Type serviceType) => null;

		public object? GetKeyedService(Type serviceType, object? serviceKey) => null;

		public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
			throw new InvalidOperationException($"No service for {serviceType}.");
	}
}
