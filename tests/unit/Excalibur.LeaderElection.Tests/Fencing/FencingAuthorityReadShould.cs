// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Consul;

namespace Excalibur.LeaderElection.Tests.Fencing;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class FencingAuthorityReadShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservePreCancellationBeforeReadingInMemoryAuthority(bool validate)
    {
        var store = new InMemoryFencingTokenProvider(new InMemoryLeaderElectionSharedState());
        (await store.IssueTokenAsync("resource", CancellationToken.None)).ShouldBe(1L);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var failure = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            if (validate) { await store.ValidateTokenAsync("resource", 1, cancellation.Token); }
            else { await store.GetTokenAsync("resource", cancellation.Token); }
        });

        failure.CancellationToken.ShouldBe(cancellation.Token);
        (await store.GetTokenAsync("resource", CancellationToken.None)).ShouldBe(1L);
    }

    [Fact]
    public async Task DemandTheRedisPrimaryRatherThanAllowReplicaFallback()
    {
        using var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        RedisKey observedKey = default;
        CommandFlags? observedFlags = null;
        A.CallTo(() => database.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
            .ReturnsLazily((RedisKey key, CommandFlags flags) =>
            {
                observedKey = key;
                observedFlags = flags;
                return Task.FromResult((RedisValue)9L);
            });

        var store = new RedisFencingTokenProvider(connection);
        (await store.GetTokenAsync("resource", CancellationToken.None)).ShouldBe(9L);

        observedKey.ShouldBe((RedisKey)"fencing:resource");
        observedFlags.ShouldBe(CommandFlags.DemandMaster);
    }

    [Fact]
    public async Task BindConsistentCounterReadsAndWritesToTheCapturedConsulAuthority()
    {
        using var client = A.Fake<IConsulClient>();
        var endpoint = A.Fake<IKVEndpoint>();
        A.CallTo(() => client.KV).Returns(endpoint);
        var options = new ConsulLeaderElectionOptions { KeyPrefix = "prefix", Datacenter = "dc-a", Token = "token-a" };
        var store = new ConsulFencingTokenProvider(client, Options.Create(options));
        options.Datacenter = "changed";
        options.Token = "changed";
        using var cancellation = new CancellationTokenSource();
        var reads = new List<QueryOptions?>();
        var writes = new List<WriteOptions?>();
        QueryResult<KVPair> Read(string key, QueryOptions? query, CancellationToken token)
        {
            key.ShouldBe("prefix/fencing/resource");
            token.ShouldBe(cancellation.Token);
            reads.Add(query);
            return new QueryResult<KVPair> { Response = new KVPair(key) { Value = Encoding.UTF8.GetBytes("9"), ModifyIndex = 5 } };
        }
        WriteResult<bool> Write(KVPair pair, WriteOptions? write, CancellationToken token)
        {
            pair.Key.ShouldBe("prefix/fencing/resource");
            pair.ModifyIndex.ShouldBe(5UL);
            Encoding.UTF8.GetString(pair.Value).ShouldBe("10");
            token.ShouldBe(cancellation.Token);
            writes.Add(write);
            return new WriteResult<bool> { Response = true };
        }
        // Both old and strengthened overloads perform the identical workload; old behavior reaches semantic assertions.
        A.CallTo(() => endpoint.Get(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken token) => Task.FromResult(Read(key, null, token)));
        A.CallTo(() => endpoint.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._))
            .ReturnsLazily((string key, QueryOptions query, CancellationToken token) => Task.FromResult(Read(key, query, token)));
        A.CallTo(() => endpoint.CAS(A<KVPair>._, A<CancellationToken>._))
            .ReturnsLazily((KVPair pair, CancellationToken token) => Task.FromResult(Write(pair, null, token)));
        A.CallTo(() => endpoint.CAS(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily((KVPair pair, WriteOptions write, CancellationToken token) => Task.FromResult(Write(pair, write, token)));

        (await store.GetTokenAsync("resource", cancellation.Token)).ShouldBe(9L);
        (await store.IssueTokenAsync("resource", cancellation.Token)).ShouldBe(10L);

        reads.Count.ShouldBe(2);
        foreach (var query in reads)
        {
            query.ShouldNotBeNull();
            query.Consistency.ShouldBe(ConsistencyMode.Consistent);
            query.Datacenter.ShouldBe("dc-a");
            query.Token.ShouldBe("token-a");
        }
        var writeOptions = writes.ShouldHaveSingleItem();
        writeOptions.ShouldNotBeNull();
        writeOptions.Datacenter.ShouldBe("dc-a");
        writeOptions.Token.ShouldBe("token-a");
    }
}
