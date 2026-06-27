// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// NON-SKIPPED real-infra regression lock for bead <c>ne79ro</c> (sprint 855, FR-A2): the wired Azure
/// Service Bus <see cref="ITransportReceiver"/> MUST deliver messages in <b>per-session FIFO order</b>
/// when <c>Processor.RequiresSession</c> is enabled — observed on the real Service Bus emulator, not a
/// mock (NFR-1 / <c>verify-against-real-infra-not-mock</c>).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>). Backend's ne79ro impl routes
/// the kek7vm-wired keyed <see cref="ITransportReceiver"/> to a session-aware seam
/// (<c>ServiceBusSessionReceiverSeam</c>, <c>AcceptNextSessionAsync</c>) when <c>Processor.RequiresSession</c>
/// is set; it accepts one session at a time, returns that session's messages in broker FIFO order, and
/// releases on drain so the next receive locks another session — preserving intra-session order while
/// making cross-session progress.
/// </para>
/// <para>
/// <b>Real-infra, NON-SKIPPED:</b> runs against the ASB emulator via
/// <see cref="AzureServiceBusContainerFixture"/> against the pre-created session-enabled <c>session-queue</c>
/// (<c>RequiresSession:true</c>). <c>DockerAvailable</c> is asserted (hard requirement, NFR-1) — the
/// fixture's graceful-degradation is flipped off (spec §8.4) so the suite can't silently skip the
/// real-infra proof. Asserts the ordering <b>observed on the real broker</b>, not that an option was set.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-ne79ro surface):</b> ASB requires a <i>session</i> receiver to consume a
/// session-enabled queue — the pre-fix non-session receiver cannot consume <c>session-queue</c> at all
/// (no ordered delivery), so the per-session FIFO assertion fails. GREEN once the session seam is wired.
/// </para>
/// </remarks>
[Collection(ContainerCollections.AzureServiceBus)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AzureServiceBus")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AzureServiceBusSessionOrderingIntegrationShould
{
	private const string SessionQueue = "session-queue";
	private const string TransportName = "ne79ro-sessions";
	private const string OrderedSession = "ordered-session";
	private const int MessageCount = 5;

	private readonly AzureServiceBusContainerFixture _fixture;

	public AzureServiceBusSessionOrderingIntegrationShould(AzureServiceBusContainerFixture fixture)
		=> _fixture = fixture;

	[Fact]
	public async Task DeliverMessagesInPerSessionFifoOrder()
	{
		// NON-SKIPPED: real-infra fidelity is the load-bearing bar (NFR-1). The fixture no longer
		// degrades gracefully (§8.4) — if the emulator can't start, this fails rather than skips.
		_fixture.DockerAvailable.ShouldBeTrue("ASB emulator must be available — real-infra session-ordering proof (NFR-1)");

		// Arrange — wire the transport with session consumption enabled (ne79ro), via the PUBLIC fluent
		// builder. NOTE: requires the additive IAzureServiceBusProcessorBuilder.RequiresSession(bool)
		// builder method (flagged to Backend, ne79ro thread 17044 — the option was builder-unreachable).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAzureServiceBusTransport(TransportName, sb => sb
			.ConnectionString(_fixture.ConnectionString)
			.ConfigureSender(s => s.DefaultEntity(SessionQueue)) // wired receiver resolves entity = Sender.DefaultEntityName ?? name
			.ConfigureProcessor(p => p.RequiresSession(true).DefaultEntity(SessionQueue)));

		await using var provider = services.BuildServiceProvider();
		var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

		// Publish MessageCount messages to a single session, in a known order.
		await using (var sender = _fixture.Client.CreateSender(SessionQueue))
		{
			for (var seq = 0; seq < MessageCount; seq++)
			{
				var body = seq.ToString(CultureInfo.InvariantCulture);
				await sender.SendMessageAsync(new ServiceBusMessage(body) { SessionId = OrderedSession })
					.ConfigureAwait(false);
			}
		}

		// Act — drain the session via the wired session-aware receiver (locks the session, returns its
		// messages in broker FIFO order).
		var receivedOrder = new List<string>(MessageCount);
		var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
		var emptyStreak = 0;

		while (receivedOrder.Count < MessageCount && DateTime.UtcNow < deadline)
		{
			var batch = await receiver.ReceiveAsync(maxMessages: MessageCount, CancellationToken.None).ConfigureAwait(false);
			if (batch.Count == 0)
			{
				if (++emptyStreak > 10)
				{
					break;
				}

				continue;
			}

			emptyStreak = 0;
			foreach (var message in batch)
			{
				receivedOrder.Add(Encoding.UTF8.GetString(message.Body.Span));
				await receiver.AcknowledgeAsync(message, CancellationToken.None).ConfigureAwait(false);
			}
		}

		// Assert — all messages delivered in strict per-session FIFO order (the broker guarantee the
		// session seam preserves), observed on the real emulator.
		var expected = Enumerable.Range(0, MessageCount)
			.Select(i => i.ToString(CultureInfo.InvariantCulture))
			.ToList();
		receivedOrder.ShouldBe(
			expected,
			$"session '{OrderedSession}' must arrive in FIFO order; got [{string.Join(", ", receivedOrder)}]");
	}
}
