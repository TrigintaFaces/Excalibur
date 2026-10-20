// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Packets;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class MqttReceiverSettlementConformanceShould
{
    [Theory]
    [InlineData(MessageAction.Acknowledge)]
    [InlineData(MessageAction.Reject)]
    [InlineData(MessageAction.Requeue)]
    public async Task RefuseUnknownReceiptAndStillAcknowledgeAnUnrelatedDelivery(MessageAction action)
    {
        var fixture = new ReceiverFixture();
        await fixture.VerifyUnknownAsync(action);
    }

    [Theory]
    [InlineData(MessageAction.Acknowledge)]
    [InlineData(MessageAction.Reject)]
    public async Task KeepHealthyBatchPeerIndependentAfterNativeFailure(MessageAction action)
    {
        var fixture = new ReceiverFixture();
        await fixture.VerifyBatchAsync(action);
    }

    private sealed class ReceiverFixture : TransportReceiverConformanceTests
    {
        private readonly IMqttClient _client = A.Fake<IMqttClient>();
        private readonly IMqttConnectionProvider _connections = A.Fake<IMqttConnectionProvider>();
        private readonly Queue<MqttApplicationMessageReceivedEventArgs> _pending = new();
        private readonly List<int> _acknowledgementAttempts = [];
        private Exception? _firstFault;
        private int _issued;
        private bool _connected;

        public ReceiverFixture()
        {
            A.CallTo(() => _connections.CreateClient()).Returns(_client);
            A.CallTo(() => _connections.BuildClientOptions("sub")).Returns(
                new MqttClientOptionsBuilder().WithTcpServer("fixture")
                    .WithCleanStart(false).WithSessionExpiryInterval(3600).Build());
            A.CallTo(() => _client.IsConnected).ReturnsLazily(() => _connected);
            A.CallTo(() => _client.ConnectAsync(A<MqttClientOptions>._, A<CancellationToken>._))
                .Invokes(() => _connected = true);
            A.CallTo(() => _client.SubscribeAsync(A<MqttClientSubscribeOptions>._, A<CancellationToken>._))
                .Invokes(() =>
                {
                    while (_pending.TryDequeue(out var args))
                    {
                        _client.ApplicationMessageReceivedAsync += Raise.FreeForm<Func<MqttApplicationMessageReceivedEventArgs, Task>>.With(args);
                    }
                });
        }

        protected override Task<ITransportReceiver> CreateReceiverAsync() => Task.FromResult<ITransportReceiver>(
            new MqttTransportReceiver(_connections, new MqttOptions { Host = "fixture", Topic = "orders", ClientId = "fixture" },
                NullLogger<MqttTransportReceiver>.Instance));

        protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
        {
            foreach (var message in messages)
            {
                var key = ++_issued;
                var args = new MqttApplicationMessageReceivedEventArgs("fixture",
                    new MqttApplicationMessage { Topic = "orders", Payload = new System.Buffers.ReadOnlySequence<byte>(message.Body), QualityOfServiceLevel = MqttTransportSender.MapQos(MqttQualityOfService.AtLeastOnce) },
                    new MqttPublishPacket(), (_, _) =>
                    {
                        _acknowledgementAttempts.Add(key);
                        return key == 1 && _firstFault is not null ? Task.FromException(_firstFault) : Task.CompletedTask;
                    });
                if (_connected)
                {
                    _client.ApplicationMessageReceivedAsync += Raise.FreeForm<Func<MqttApplicationMessageReceivedEventArgs, Task>>.With(args);
                }
                else
                {
                    _pending.Enqueue(args);
                }
            }
            return Task.CompletedTask;
        }

        public Task VerifyUnknownAsync(MessageAction action) => VerifySettlementRefusal(action,
            _ => Task.FromResult(new TransportReceivedMessage { Id = "never-issued" }),
            TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent,
            (_, _) => ObserveAsync([]), (_, _) => ObserveAsync([1]));

        public Task VerifyBatchAsync(MessageAction action) => VerifySettlementBatchRetry(action, MessageAction.Acknowledge,
            (_, _) =>
            {
                _firstFault = new IOException("Fixture acknowledgement response lost");
                return Task.FromResult(_firstFault);
            }, Permanent(), Permanent(), (_, _) => ObserveAsync([1]), (_, _) => ObserveAsync([1, 2]), TimeSpan.FromSeconds(5));

        private static SettlementExpectation Permanent() => SettlementExpectation.Refused(
            TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent);

        private Task ObserveAsync(int[] expected)
        {
            _acknowledgementAttempts.ToArray().ShouldBe(expected);
            A.CallTo(() => _client.ConnectAsync(A<MqttClientOptions>._, A<CancellationToken>._)).MustHaveHappened(expected.Length == 0 ? 0 : 1, Times.Exactly);
            A.CallTo(() => _client.SubscribeAsync(A<MqttClientSubscribeOptions>._, A<CancellationToken>._)).MustHaveHappened(expected.Length == 0 ? 0 : 1, Times.Exactly);
            return Task.CompletedTask;
        }
    }
}
