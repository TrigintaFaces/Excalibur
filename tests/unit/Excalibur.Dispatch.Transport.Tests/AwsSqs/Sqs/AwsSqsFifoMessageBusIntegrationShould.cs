// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// Real-infrastructure regression lock (bead i0rr4m, FR-B4) proving that
/// <see cref="AwsSqsMessageBus"/> actually wires SQS FIFO ordering and deduplication
/// identifiers from the configured <see cref="AwsSqsFifoOptions"/> selectors onto the
/// outgoing <c>SendMessageRequest</c> for all three publish paths
/// (<see cref="IDispatchAction"/>, <see cref="IDispatchEvent"/>, <see cref="IDispatchDocument"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bug under lock</b>: <c>ConfigureFifo</c> captured the group-id/dedup-id selectors into
/// options, but the publish paths built their <c>SendMessageRequest</c> and never read
/// <see cref="AwsSqsFifoOptions"/>, so <c>MessageGroupId</c>/<c>MessageDeduplicationId</c> were
/// never set and FIFO was silently inert. The fix routes every publish path through
/// <c>ApplyFifo</c> (see <c>AwsSqsMessageBus.cs</c> lines 168-179, called at 92/117/142).
/// </para>
/// <para>
/// <b>Non-vacuity</b>: this lock runs against a real LocalStack FIFO queue (a real external
/// system with server-side validation — <c>verify-against-real-infra</c>), not a mocked
/// <c>IAmazonSQS</c>. A real SQS FIFO queue <i>rejects</i> a send that omits <c>MessageGroupId</c>
/// (and, with content-based dedup disabled, omits <c>MessageDeduplicationId</c>). So on the
/// pre-fix code (publish paths ignore <see cref="AwsSqsFifoOptions"/> ⇒ both ids null/unset) the
/// publish itself throws ⇒ <b>RED</b>; on the FifoOptions-wired paths the send succeeds, the group
/// id round-trips on receive, and the dedup id is honored by SQS (duplicate suppressed) ⇒
/// <b>GREEN</b>. A mocked client would have certified the broken provider (it never runs SQS's
/// server-side FIFO validation). Production RED-proof is deferred to post-commit — the impl file
/// (<c>AwsSqsMessageBus.cs</c>) is reserved by another lane and is not mutated here.
/// </para>
/// <para>
/// <b>Direct construction (bead rlskyu, P1)</b>: <c>AwsSqsMessageBus</c> requires a concrete
/// <see cref="AwsSqsOptions"/> that neither <c>AddAwsSqsTransport</c> nor <c>AddAwsMessageBus</c>
/// registers, so <c>GetRequiredService&lt;AwsSqsMessageBus&gt;()</c> throws on the public DI path.
/// FR-B4 is therefore proven by DIRECT construction of the bus, isolating this lock from rlskyu.
/// </para>
/// <para>
/// <b>Test-home rationale (seam note)</b>: <c>AwsSqsMessageBus</c> is <c>internal sealed</c>. Its
/// assembly only grants <c>InternalsVisibleTo</c> to a fixed set that includes this project
/// (<c>Excalibur.Dispatch.Transport.Tests</c>) but NOT <c>Excalibur.Dispatch.Integration.Tests</c>.
/// Because the production csproj must not be modified, this real-LocalStack lock lives here — the
/// only project that can both construct the internal bus and reach the shared
/// <see cref="AwsSqsContainerFixture"/> (via <c>Tests.Shared</c>). It is tagged with the
/// Integration category so trait-based shard selection runs it on the integration shard, not the
/// unit shard.
/// </para>
/// <para>
/// <b>Assertion approach</b>: real round-trip (preferred over inspecting the built request).
/// <c>MessageGroupId</c> is asserted via the strongly-typed property on the received SQS
/// <c>Message</c> (rock-solid on LocalStack). <c>MessageDeduplicationId</c> wiring is asserted
/// behaviorally and reliably: two identical sends with a constant dedup-id selector cause SQS to
/// suppress the duplicate, so exactly one message is delivered. The bus is given its own
/// <see cref="AmazonSQSClient"/> so disposing the bus does not dispose the fixture's shared client.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AwsSqs")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsFifoMessageBusIntegrationShould : IClassFixture<AwsSqsContainerFixture>
{
	private const string ExpectedGroupId = "i0rr4m-group";
	private const string ExpectedDeduplicationId = "i0rr4m-dedup";

	private readonly AwsSqsContainerFixture _fixture;

	public AwsSqsFifoMessageBusIntegrationShould(AwsSqsContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task PublishEvent_OnRealFifoQueue_SetsConfiguredMessageGroupId()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"i0rr4m SQS FIFO group/dedup is real-infra wiring — never skipped");

		var queueUrl = await CreateFifoQueueAsync().ConfigureAwait(false);
		await using var bus = CreateBus(queueUrl);

		var evt = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();

		// Pre-fix: FIFO send without MessageGroupId/MessageDeduplicationId is rejected by SQS ⇒ throws here.
		await bus.PublishAsync(evt, context, CancellationToken.None).ConfigureAwait(false);

		var received = await ReceiveOneAsync(queueUrl).ConfigureAwait(false);
		received.ShouldNotBeNull();
		GroupIdOf(received!).ShouldBe(ExpectedGroupId);
	}

	[Fact]
	public async Task PublishAction_OnRealFifoQueue_SetsConfiguredMessageGroupId()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"i0rr4m SQS FIFO group/dedup is real-infra wiring — never skipped");

		var queueUrl = await CreateFifoQueueAsync().ConfigureAwait(false);
		await using var bus = CreateBus(queueUrl);

		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();

		await bus.PublishAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		var received = await ReceiveOneAsync(queueUrl).ConfigureAwait(false);
		received.ShouldNotBeNull();
		GroupIdOf(received!).ShouldBe(ExpectedGroupId);
	}

	[Fact]
	public async Task PublishDocument_OnRealFifoQueue_SetsConfiguredMessageGroupId()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"i0rr4m SQS FIFO group/dedup is real-infra wiring — never skipped");

		var queueUrl = await CreateFifoQueueAsync().ConfigureAwait(false);
		await using var bus = CreateBus(queueUrl);

		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();

		await bus.PublishAsync(doc, context, CancellationToken.None).ConfigureAwait(false);

		var received = await ReceiveOneAsync(queueUrl).ConfigureAwait(false);
		received.ShouldNotBeNull();
		GroupIdOf(received!).ShouldBe(ExpectedGroupId);
	}

	[Fact]
	public async Task PublishEvent_OnRealFifoQueue_HonorsConfiguredDeduplicationId_SuppressingDuplicate()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"i0rr4m SQS FIFO group/dedup is real-infra wiring — never skipped");

		var queueUrl = await CreateFifoQueueAsync().ConfigureAwait(false);
		await using var bus = CreateBus(queueUrl);

		var evt = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();

		// Two identical sends with a constant dedup-id selector. The configured (group, dedup) pair
		// makes the second send a duplicate within SQS's 5-minute window — SQS suppresses it.
		// Pre-fix the dedup id is never set, so SQS would not deduplicate (and the send would in fact
		// be rejected for missing the required FIFO ids), making this assertion fail.
		await bus.PublishAsync(evt, context, CancellationToken.None).ConfigureAwait(false);
		await bus.PublishAsync(evt, context, CancellationToken.None).ConfigureAwait(false);

		var receivedCount = 0;
		string? groupIdSeen = null;
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var response = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = 10,
				WaitTimeSeconds = 5,
				MessageSystemAttributeNames = ["All"],
			}).ConfigureAwait(false);

			foreach (var message in response.Messages ?? [])
			{
				receivedCount++;
				groupIdSeen = GroupIdOf(message);
				await _fixture.SqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle).ConfigureAwait(false);
			}
		}

		// Exactly one message survives deduplication.
		receivedCount.ShouldBe(1);
		groupIdSeen.ShouldBe(ExpectedGroupId);
	}

	private async Task<string> CreateFifoQueueAsync()
	{
		var queueName = $"i0rr4m-fifo-{Guid.NewGuid():N}.fifo";
		var response = await _fixture.SqsClient.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
			Attributes = new Dictionary<string, string>
			{
				["FifoQueue"] = "true",
				["ContentBasedDeduplication"] = "false",
			},
		}).ConfigureAwait(false);
		return response.QueueUrl;
	}

	/// <summary>
	/// Constructs the bus directly (bead rlskyu) with FIFO selectors that produce known
	/// group/dedup identifiers, against the configured FIFO queue. The bus is given its OWN
	/// SQS client (pointed at the same LocalStack endpoint) so its disposal of that client does
	/// not tear down the fixture's shared <see cref="AwsSqsContainerFixture.SqsClient"/>.
	/// </summary>
	private AwsSqsMessageBus CreateBus(string queueUrl)
	{
		var serializer = A.Fake<IPayloadSerializer>();
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns([1, 2, 3]);

		var options = new AwsSqsOptions { QueueUrl = new Uri(queueUrl), UseFifoQueue = true };

		var fifoOptions = new AwsSqsFifoOptions
		{
			ContentBasedDeduplication = false,
			MessageGroupIdSelector = _ => ExpectedGroupId,
			DeduplicationIdSelector = _ => ExpectedDeduplicationId,
		};

		var busClient = new AmazonSQSClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSQSConfig { ServiceURL = _fixture.ConnectionString });

		return new AwsSqsMessageBus(
			busClient,
			serializer,
			Microsoft.Extensions.Options.Options.Create(options),
			Microsoft.Extensions.Options.Options.Create(fifoOptions),
			NullLogger<AwsSqsMessageBus>.Instance);
	}

	private async Task<Message?> ReceiveOneAsync(string queueUrl)
	{
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var response = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = 1,
				WaitTimeSeconds = 5,
				MessageSystemAttributeNames = ["All"],
				MessageAttributeNames = ["All"],
			}).ConfigureAwait(false);

			var messages = response.Messages ?? [];
			if (messages.Count > 0)
			{
				return messages[0];
			}
		}

		return null;
	}

	/// <summary>
	/// Reads the FIFO group id from a received SQS message's system <c>Attributes</c> (populated by
	/// <c>MessageSystemAttributeNames = ["All"]</c> on receive). Read via the attribute dictionary —
	/// not a strongly-typed property — so the assertion is independent of the AWSSDK.SQS version the
	/// test project resolves (the <c>Message.MessageGroupId</c> property is not present in every
	/// AWSSDK major). Mirrors the production round-trip convention in <c>AwsSqsCloudEventAdapter</c>
	/// (<c>message.Attributes["MessageGroupId"]</c>). Returns <see langword="null"/> if absent.
	/// </summary>
	private static string? GroupIdOf(Message message) =>
		message.Attributes is { } attributes && attributes.TryGetValue("MessageGroupId", out var groupId)
			? groupId
			: null;
}
