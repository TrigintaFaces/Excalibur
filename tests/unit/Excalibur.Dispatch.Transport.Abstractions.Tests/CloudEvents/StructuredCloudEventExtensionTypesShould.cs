// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

/// <summary>Checks JSON token kinds through the public shared transport encoding path.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Conformance")]
public sealed class StructuredCloudEventExtensionTypesShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveBooleanExtensionTokenKind(bool value)
    {
        var extension = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
        var cloudEvent = CreateEvent();
        cloudEvent[extension] = value;
        var inner = new RecordingSender();

        await inner.WithCloudEventEncoding().SendAsync(
            TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);

        using var document = JsonDocument.Parse(inner.Sent.ShouldHaveSingleItem().Body);
        var actual = document.RootElement.GetProperty(extension.Name);
        actual.ValueKind.ShouldBe(value ? JsonValueKind.True : JsonValueKind.False);
        actual.GetBoolean().ShouldBe(value);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task PreserveIntegerExtensionTokenKind(int value)
    {
        var extension = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);
        var cloudEvent = CreateEvent();
        cloudEvent[extension] = value;
        var inner = new RecordingSender();

        await inner.WithCloudEventEncoding().SendAsync(
            TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);

        using var document = JsonDocument.Parse(inner.Sent.ShouldHaveSingleItem().Body);
        var actual = document.RootElement.GetProperty(extension.Name);
        actual.ValueKind.ShouldBe(JsonValueKind.Number);
        actual.GetInt32().ShouldBe(value);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("22")]
    [InlineData("0")]
    [InlineData("")]
    public async Task PreserveStringExtensionWithoutInferringItsTypeFromText(string value)
    {
        var extension = CloudEventAttribute.CreateExtension("conformancelabel", CloudEventAttributeType.String);
        var cloudEvent = CreateEvent();
        cloudEvent[extension] = value;
        var inner = new RecordingSender();

        await inner.WithCloudEventEncoding().SendAsync(
            TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);

        using var document = JsonDocument.Parse(inner.Sent.ShouldHaveSingleItem().Body);
        var actual = document.RootElement.GetProperty(extension.Name);
        actual.ValueKind.ShouldBe(JsonValueKind.String);
        actual.GetString().ShouldBe(value);
    }

    [Fact]
    public async Task PreserveMixedExtensionTypesInBatchAndLeaveOrdinaryTrafficUntouched()
    {
        var flag = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
        var count = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);
        var label = CloudEventAttribute.CreateExtension("conformancelabel", CloudEventAttributeType.String);
        var cloudEvent = CreateEvent();
        cloudEvent[flag] = false;
        cloudEvent[count] = 22;
        cloudEvent[label] = "22";
        var ordinary = TransportMessage.FromString("unchanged");
        ordinary.ContentType = "text/plain";
        var inner = new RecordingSender();

        await inner.WithCloudEventEncoding().SendBatchAsync(
            [TransportMessage.FromString("original").WithCloudEvent(cloudEvent), ordinary], CancellationToken.None);

        inner.Sent.Count.ShouldBe(2);
        using var document = JsonDocument.Parse(inner.Sent[0].Body);
        document.RootElement.GetProperty(flag.Name).ValueKind.ShouldBe(JsonValueKind.False);
        document.RootElement.GetProperty(count.Name).ValueKind.ShouldBe(JsonValueKind.Number);
        document.RootElement.GetProperty(label.Name).ValueKind.ShouldBe(JsonValueKind.String);
        inner.Sent[1].ShouldBeSameAs(ordinary);
        inner.Sent[1].ContentType.ShouldBe("text/plain");
        Encoding.UTF8.GetString(inner.Sent[1].Body.Span).ShouldBe("unchanged");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveCustomPrimitiveValidationBeforeSendingAndAllowAValidRetry(bool booleanType)
    {
        var acceptValue = true;
        var extension = CloudEventAttribute.CreateExtension(
            "conformancevalidated",
            booleanType ? CloudEventAttributeType.Boolean : CloudEventAttributeType.Integer,
            _ =>
            {
                if (!acceptValue)
                {
                    throw new InvalidOperationException("Attribute validation currently refuses this value.");
                }
            });
        var cloudEvent = CreateEvent();
        cloudEvent[extension] = booleanType ? (object)false : 0;
        var message = TransportMessage.FromString("original").WithCloudEvent(cloudEvent);
        message.ContentType = "text/plain";
        var inner = new RecordingSender();
        var sender = inner.WithCloudEventEncoding();
        acceptValue = false;

        await Should.ThrowAsync<ArgumentException>(() => sender.SendAsync(message, CancellationToken.None));

        inner.Sent.ShouldBeEmpty();
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("original");
        message.ContentType.ShouldBe("text/plain");
        message.Properties.Values.ShouldContain(value => ReferenceEquals(value, cloudEvent));

        acceptValue = true;
        await sender.SendAsync(message, CancellationToken.None);
        inner.Sent.ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.Properties.Values.ShouldNotContain(value => value is CloudEvent);
    }

    [Fact]
    public async Task NeverPublishAnExtensionAsASecondPayloadMember()
    {
        // The pinned SDK's custom-validator overload permits this otherwise-reserved name.
        var reserved = CloudEventAttribute.CreateExtension("data", CloudEventAttributeType.String, static _ => { });
        var cloudEvent = CreateEvent();
        cloudEvent[reserved] = "extension-cannot-be-payload";
        var inner = new RecordingSender();
        var message = TransportMessage.FromString("original").WithCloudEvent(cloudEvent);

        try
        {
            await inner.WithCloudEventEncoding().SendAsync(message, CancellationToken.None);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            // Refusing the malformed event before transport I/O is a valid safety outcome.
            inner.Sent.ShouldBeEmpty();
            Encoding.UTF8.GetString(message.Body.Span).ShouldBe("original");
            return;
        }

        using var document = JsonDocument.Parse(inner.Sent.ShouldHaveSingleItem().Body);
        var payloadMembers = document.RootElement.EnumerateObject().Where(static property => property.Name == "data").ToArray();
        payloadMembers.Length.ShouldBe(1, "an extension must not create an ambiguous duplicate payload member");
        payloadMembers[0].Value.GetString().ShouldBe("payload");
    }

    private static CloudEvent CreateEvent() => new()
    {
        Id = "extension-token-kind-1",
        Type = "com.excalibur.conformance",
        Source = new Uri("https://example.test/extension-types"),
        Data = "payload",
    };

    private sealed class RecordingSender : ITransportSender
    {
        public List<TransportMessage> Sent { get; } = [];

        public string Destination => "recording";

        public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
        }

        public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
        {
            Sent.AddRange(messages);
            return Task.FromResult(new BatchSendResult());
        }

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
