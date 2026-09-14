// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;
using Amazon.SQS.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Decorators;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Binds the whole inbound round trip for a transport: what its adapter EMITS, the receiver a consumer
/// RESOLVES decodes back into a CloudEvent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Neither half is named by this file, and that is the point.</b> The attribute spellings come from the
/// message the real adapter produced, and the decoding binding comes from the transport's own
/// registration - not from a literal here. An earlier version of this file supplied the binding itself and
/// was therefore GREEN against the exact defect it was written to lock: reverting the registration to the
/// one that shipped the encode-without-decode fault left it passing, because the file was never asking the
/// registration anything.
/// </para>
/// <para>
/// <b>Why resolving the receiver is not enough on its own.</b> The sibling resolution arms assert that the
/// registration wrapped the receiver in the decoding decorator. That type was already present before the
/// fix - only the binding ARGUMENT changed - so a type assertion is green on both sides of the defect and
/// cannot discriminate it. The decoded event is the property that can.
/// </para>
/// <para>
/// <b>No infrastructure.</b> The vendor client is faked and hands back exactly the attributes the adapter
/// wrote, so the transport's own receive path maps them and the registered decoder reads them. Nothing
/// here connects to a broker.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventRoundTripShould
{
    private const string TransportName = "roundtrip";

    /// <summary>
    /// LIVENESS. What the SQS adapter emits, the receiver SQS registers decodes back into the event.
    /// </summary>
    [Fact]
    public async Task Decode_what_the_Sqs_adapter_emits()
    {
        var adapter = new AwsSqsCloudEventAdapter(
            Microsoft.Extensions.Options.Options.Create(EventOptions()),
            NullLogger<AwsSqsCloudEventAdapter>.Instance);

        var emitted = await adapter.ToTransportMessageAsync(
            Event(), CloudEventMode.Binary, CancellationToken.None);

        var services = new ServiceCollection();
        _ = services.AddLogging();

        // Registered BEFORE the transport, which uses TryAddKeyedSingleton for its client - so this fake
        // wins and every other part of the registration stays exactly as a consumer gets it.
        _ = services.AddKeyedSingleton(TransportName, SqsClientReturning(emitted));
        _ = services.AddAwsSqsTransport(TransportName, _ => { });

        await using var provider = services.BuildServiceProvider();
        var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

        var received = await receiver.ReceiveAsync(1, CancellationToken.None);

        var providerData = received.ShouldHaveSingleItem().ProviderData;

        providerData.ShouldContainKey(
            CloudEventDecodingTransportReceiver.CloudEventProviderDataKey,
            "SQS publishes these attribute names and the receiver its own registration builds could "
            + "not read them back, so a CloudEvent this framework emitted arrives at a consumer of "
            + "this framework as ordinary traffic - indistinguishable from a message that never "
            + $"carried CloudEvents markers. Names actually emitted: {Names(emitted)}");

        // PRESENCE IS A WEAKER CLAIM THAN THE CAPABILITY. A decoder that attached an empty or wrong
        // event satisfies the key check above completely. The attributes are what a consumer actually
        // reads off the decoded event, so the round trip is only closed once the values that went out
        // are the values that come back - which is the property the requirement states, and the one a
        // per-attribute spelling bug (the defect class this whole seam guards) would break while
        // leaving the key in place.
        var decoded = providerData[CloudEventDecodingTransportReceiver.CloudEventProviderDataKey]
            .ShouldBeOfType<CloudEvent>();

        var sent = Event();
        decoded.Id.ShouldBe(sent.Id, "the decoded event's id is not the one the adapter emitted");
        decoded.Type.ShouldBe(sent.Type, "the decoded event's type is not the one the adapter emitted");
        decoded.Source.ShouldBe(sent.Source, "the decoded event's source is not the one the adapter emitted");
    }

    /// <summary>
    /// SAFETY. A message carrying none of the adapter's attributes is not reported as a CloudEvent.
    /// </summary>
    /// <remarks>
    /// The pair to the arm above, and not a formality: a decoder that attached an event to every inbound
    /// message would satisfy the liveness arm completely while telling a consumer nothing.
    /// </remarks>
    [Fact]
    public async Task Attach_no_event_to_a_message_that_carries_none()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddKeyedSingleton(TransportName, SqsClientReturning(new SendMessageRequest
        {
            MessageBody = "ordinary traffic",
            MessageAttributes = [],
        }));
        _ = services.AddAwsSqsTransport(TransportName, _ => { });

        await using var provider = services.BuildServiceProvider();
        var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

        var received = await receiver.ReceiveAsync(1, CancellationToken.None);

        received.ShouldHaveSingleItem().ProviderData.ShouldNotContainKey(
            CloudEventDecodingTransportReceiver.CloudEventProviderDataKey,
            "a plain message must stay plain - reporting an event here would make the liveness arm above "
            + "pass for a decoder that never read anything");
    }

    /// <summary>
    /// A client whose receive call hands back one message carrying exactly what the adapter wrote.
    /// </summary>
    private static IAmazonSQS SqsClientReturning(SendMessageRequest emitted)
    {
        var client = A.Fake<IAmazonSQS>();

        _ = A.CallTo(() => client.ReceiveMessageAsync(
                A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "m-1",
                        ReceiptHandle = "r-1",
                        Body = emitted.MessageBody,
                        MessageAttributes = emitted.MessageAttributes,
                    },
                ],
            });

        return client;
    }

    private static string Names(SendMessageRequest emitted) =>
        string.Join(", ", emitted.MessageAttributes.Keys);

    private static CloudEventOptions EventOptions() =>
        new() { DefaultSource = new Uri("https://test.excalibur.io") };

    private static CloudEvent Event() =>
        new(CloudEventsSpecVersion.V1_0)
        {
            Id = "evt-1",
            Type = "order.placed",
            Source = new Uri("https://source.excalibur.io"),
            Data = "payload",
            DataContentType = "text/plain",
        };
}
