// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using CloudNative.CloudEvents;

using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the Kafka transport.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion lives in the kit. This class supplies only what is Kafka-specific.
/// </para>
/// <para>
/// <b>The encoder is RESOLVED, not constructed.</b> Kafka's adapter is <c>internal</c> and reaches this
/// project only through the public interface its own registration binds, which is the better seam
/// regardless of visibility: constructing an encoder by hand tests an encoder nobody runs, while
/// resolving one tests the encoder a consumer gets. Where a transport's adapter happens to be public,
/// prefer resolving it here anyway.
/// </para>
/// <para>
/// <b>CloudEvents is opt-in on this transport</b> — the transport registration configures the options and
/// a separate call binds the adapter. That is the supported shape, so this fixture makes both calls: a
/// consumer who wants CloudEvents makes both, and a fixture that made only one would be testing a
/// configuration no CloudEvents user runs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaCloudEventConformanceTests : CloudEventTransportConformanceTests
{
	private const string TransportName = "kafka-conformance";

	private readonly List<Confluent.Kafka.ConsumeResult<string, byte[]>> _inbound = [];

	private ServiceProvider? _provider;

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync()
	{
		var provider = BuildProvider();
		return Task.FromResult(provider.GetRequiredKeyedService<ITransportReceiver>(TransportName));
	}

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		foreach (var message in messages)
		{
			var headers = new Confluent.Kafka.Headers();

			foreach (var property in message.Properties)
			{
				headers.Add(property.Key, Encoding.UTF8.GetBytes(property.Value?.ToString() ?? string.Empty));
			}

			_inbound.Add(new Confluent.Kafka.ConsumeResult<string, byte[]>
			{
				Topic = "conformance",
				Partition = new Partition(0),
				Offset = new Offset(0),
				Message = new Confluent.Kafka.Message<string, byte[]>
				{
					Key = message.Id,
					Value = message.Body.ToArray(),
					Headers = headers,
					Timestamp = new Timestamp(DateTimeOffset.UtcNow),
				},
			});
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var adapter = BuildProvider().GetRequiredService<IKafkaCloudEventAdapter>();

		var message = await adapter.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		if (message.Headers is not null)
		{
			foreach (var header in message.Headers)
			{
				properties[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
			}
		}

		return new TransportReceivedMessage
		{
			Id = "kafka-wire-1",
			Body = Encoding.UTF8.GetBytes(message.Value ?? string.Empty),
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,
		};
	}

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var consumer = A.Fake<Confluent.Kafka.IConsumer<string, byte[]>>();

		// Consume is called per poll and must stop yielding, or the receiver spins until its window
		// closes and the arm measures a timeout rather than a decode.
		_ = A.CallTo(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() =>
			{
				if (_inbound.Count == 0)
				{
					return null!;
				}

				var next = _inbound[0];
				_inbound.RemoveAt(0);
				return next;
			});

		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Registered BEFORE the transport, which uses TryAddSingleton for its consumer, so this fake wins
		// and the rest of the registration is exactly what a consumer gets.
		_ = services.AddSingleton(consumer);
		_ = services.AddKafkaTransport(TransportName, _ => { });
		_ = services.AddCloudEventsForKafka();

		_provider = services.BuildServiceProvider();
		return _provider;
	}

	/// <summary>
	/// LIVENESS. Binary-mode round trip through Kafka's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_binary_mode_event() =>
		VerifyBinaryModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// LIVENESS. Structured-mode round trip, a different wire shape and a different decode path.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary Kafka traffic is not reported as a CloudEvent.
	/// </summary>
	[Fact]
	public Task Leave_ordinary_traffic_alone() =>
		VerifyOrdinaryTrafficIsNotReportedAsCloudEvent();

	/// <summary>
	/// SAFETY. A mode this transport declines is not decoded anyway.
	/// </summary>
	[Fact]
	public Task Decline_no_mode_it_cannot_decode() =>
		VerifyDeclinedModesDoNotDecode();
}
