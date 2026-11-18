// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Ownership")]
public sealed class RedisOutboxPayloadTransformHandoffShould
{
    [Fact]
    public void ProveRedisValueDoesNotSupplyTheProviderHandoffCopy()
    {
        var result = new byte[] { 0xE7, 0, 0xFF };
        RedisValue value = result;
        result[0] = 0;
        ((byte[]?)value).ShouldNotBeNull()[0].ShouldBe((byte)0);
    }

    [Fact]
    public async Task CopyReturnedPayloadBeforeCallingTheRedisPersistenceBoundary()
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        var result = new byte[] { 0xE7, 0, 0xFF, 0x10 };
        var expected = result.ToArray();
        var armed = false;
        var writes = 0;
        byte[]? observed = null;
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                armed.ShouldBeTrue("mutation is after the callback's handoff, never concurrent with its copy");
                writes++;
                result[0] = 0;
                keys.Length.ShouldBe(2);
                values[0].ToString().ShouldNotBe("redis-valid-context-id");
                for (var index = 2; index < values.Length; index += 2)
                {
                    if (string.Equals(values[index].ToString(), "Payload", StringComparison.Ordinal))
                    {
                        observed = ((byte[]?)values[index + 1]).ShouldNotBeNull().ToArray();
                    }
                }
                return Task.FromResult(RedisResult.Create((RedisValue)"SUCCESS"));
            });
        await using var store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "redis-handoff-control",
        }), NullLogger<RedisOutboxStore>.Instance);
        var facet = ((IOutboxStore)store).GetService(typeof(IOutboxPayloadTransformStore))
            .ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new HandoffMessage("secret");
        var context = new MessageContext(message, services) { MessageId = "redis-valid-context-id" };
        context.GetOrCreateIdentityFeature().TenantId = "redis-handoff-tenant";
        var callbacks = 0;
        await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
        {
            callbacks++;
            tenant.ShouldBe("redis-handoff-tenant");
            token.ShouldBe(CancellationToken.None);
            armed = true;
            return ValueTask.FromResult(result);
        }, CancellationToken.None).ConfigureAwait(false);
        callbacks.ShouldBe(1);
        writes.ShouldBe(1);
        result[0].ShouldBe((byte)0, "the post-handoff mutation branch actually ran");
        observed.ShouldNotBeNull().ShouldBe(expected, "the provider must own a copy before RedisValue arguments are persisted");
        A.CallTo(() => connection.Dispose()).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RefuseUndrainableStageIdentityBeforeInvokingRedisAndKeepValidAdmission(string? invalidId)
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .Returns(Task.FromResult(RedisResult.Create((RedisValue)"SUCCESS")));
        await using var store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = "localhost:6379", KeyPrefix = "redis-invalid-stage-control",
        }), NullLogger<RedisOutboxStore>.Instance);
        var invalid = new OutboundMessage("event", Array.Empty<byte>(), "destination") { Id = invalidId! };
        await Should.ThrowAsync<ArgumentException>(() => store.StageMessageAsync(invalid, CancellationToken.None).AsTask());
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .MustNotHaveHappened();
        var valid = new OutboundMessage("event", Array.Empty<byte>(), "destination");
        await store.StageMessageAsync(valid, CancellationToken.None).ConfigureAwait(false);
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .MustHaveHappenedOnceExactly();
    }

    private sealed record HandoffMessage(string Secret) : IDispatchMessage;
}
