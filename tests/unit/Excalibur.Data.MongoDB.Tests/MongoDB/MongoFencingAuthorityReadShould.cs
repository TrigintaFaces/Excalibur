// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.LeaderElection.MongoDB;

using FakeItEasy;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Shouldly;

using Xunit;

using Counter = Excalibur.LeaderElection.MongoDB.MongoDbFencingTokenProvider.FencingCounterDocument;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class MongoFencingAuthorityReadShould
{
    [Fact]
    public async Task UsePrimaryMajorityGenerationAndABoundedLinearizableReadHandle()
    {
        var client = A.Fake<IMongoClient>();
        var database = A.Fake<IMongoDatabase>();
        var original = A.Fake<IMongoCollection<Counter>>();
        var primary = A.Fake<IMongoCollection<Counter>>();
        var local = A.Fake<IMongoCollection<Counter>>();
        var writer = A.Fake<IMongoCollection<Counter>>();
        var reader = A.Fake<IMongoCollection<Counter>>();
        A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(database);
        A.CallTo(() => database.GetCollection<Counter>(A<string>._, A<MongoCollectionSettings>._)).Returns(original);
        A.CallTo(() => original.WithReadPreference(ReadPreference.Primary)).Returns(primary);
        A.CallTo(() => primary.WithReadConcern(ReadConcern.Local)).Returns(local);
        A.CallTo(() => local.WithWriteConcern(WriteConcern.WMajority)).Returns(writer);
        A.CallTo(() => writer.WithReadConcern(ReadConcern.Linearizable)).Returns(reader);
        IMongoCollection<Counter>? usedWriter = null;
        IMongoCollection<Counter>? usedReader = null;
        TimeSpan? maxTime = null;
        var mark = 9L;
        var generations = 0;
        using var cancellation = new CancellationTokenSource();
        IMongoCollection<Counter>[] handles = [original, primary, local, writer, reader];
        foreach (var handle in handles)
        {
            // Old and strengthened handles perform the same stateful work; assertions detect wrong routing.
            A.CallTo(() => handle.FindOneAndUpdateAsync(A<FilterDefinition<Counter>>._, A<UpdateDefinition<Counter>>._,
                    A<FindOneAndUpdateOptions<Counter, Counter>>._, A<CancellationToken>._))
                .ReturnsLazily((FilterDefinition<Counter> _, UpdateDefinition<Counter> _,
                    FindOneAndUpdateOptions<Counter, Counter> options, CancellationToken token) =>
                {
                    token.ShouldBe(cancellation.Token);
                    options.IsUpsert.ShouldBeTrue();
                    options.ReturnDocument.ShouldBe(ReturnDocument.After);
                    usedWriter = handle;
                    generations++;
                    return Task.FromResult(new Counter { ResourceId = "resource", Seq = ++mark });
                });
            A.CallTo(() => handle.FindAsync(A<FilterDefinition<Counter>>._,
                    A<FindOptions<Counter, Counter>>._, A<CancellationToken>._))
                .ReturnsLazily((FilterDefinition<Counter> _, FindOptions<Counter, Counter> options, CancellationToken token) =>
                {
                    token.ShouldBe(cancellation.Token);
                    usedReader = handle;
                    maxTime = options.MaxTime;
                    var cursor = A.Fake<IAsyncCursor<Counter>>();
                    A.CallTo(() => cursor.Current).Returns([new() { ResourceId = "resource", Seq = mark }]);
                    A.CallTo(() => cursor.MoveNextAsync(A<CancellationToken>._)).ReturnsNextFromSequence(true, false);
                    return Task.FromResult(cursor);
                });
        }

        var store = new MongoDbFencingTokenProvider(client, Options.Create(new MongoDbLeaderElectionOptions { TimeoutInSeconds = 7 }));
        (await store.IssueTokenAsync("resource", cancellation.Token)).ShouldBe(10L);
        (await store.GetTokenAsync("resource", cancellation.Token)).ShouldBe(10L);

        generations.ShouldBe(1);
        mark.ShouldBe(10L);
        usedWriter.ShouldBeSameAs(writer);
        usedReader.ShouldBeSameAs(reader);
        maxTime.ShouldBe(TimeSpan.FromSeconds(7));
    }
}
