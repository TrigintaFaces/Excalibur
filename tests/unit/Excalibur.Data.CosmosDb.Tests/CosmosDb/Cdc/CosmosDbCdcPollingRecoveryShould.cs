// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.CosmosDb.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "CDC")]
[Trait("Pattern", "Recovery")]
public sealed class CosmosDbCdcPollingRecoveryShould
{
    [Fact]
    public async Task RetryCheckpointRestorationAfterInitializationFails()
    {
        using var client = A.Fake<CosmosClient>();
        var database = A.Fake<Database>();
        var container = A.Fake<Container>();
        var state = A.Fake<ICosmosDbCdcStateStore>();
        using var iterator = A.Fake<FeedIterator<JsonDocument>>();
        A.CallTo(() => client.GetDatabase("db")).Returns(database);
        A.CallTo(() => database.GetContainer("source")).Returns(container);
        A.CallTo(() => container.GetChangeFeedIterator<JsonDocument>(A<ChangeFeedStartFrom>._, A<ChangeFeedMode>._, A<ChangeFeedRequestOptions>._)).Returns(iterator);
        A.CallTo(() => iterator.HasMoreResults).Returns(false);
        var calls = 0;
        var saved = CosmosDbCdcPosition.FromContinuationToken("saved");
        A.CallTo(() => state.GetPositionAsync("poll", A<CancellationToken>._))
            .ReturnsLazily(() => ++calls == 1
                ? Task.FromException<CosmosDbCdcPosition?>(new IOException("Checkpoint store unavailable"))
                : Task.FromResult<CosmosDbCdcPosition?>(saved));
        await using var processor = Create(client, state);
        await Should.ThrowAsync<IOException>(() => processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None));
        await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None);
        calls.ShouldBe(2);
        (await processor.GetCurrentPositionAsync(CancellationToken.None)).ContinuationToken.ShouldBe("saved");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraverseEmptySuccessPagesWithoutCheckpointingPastHandlerFailure(bool failHandler)
    {
        using var client = A.Fake<CosmosClient>();
        var database = A.Fake<Database>();
        var container = A.Fake<Container>();
        var state = A.Fake<ICosmosDbCdcStateStore>();
        using var iterator = new TrackingIterator();
        A.CallTo(() => client.GetDatabase("db")).Returns(database);
        A.CallTo(() => database.GetContainer("source")).Returns(container);
        A.CallTo(() => container.GetChangeFeedIterator<JsonDocument>(A<ChangeFeedStartFrom>._, A<ChangeFeedMode>._, A<ChangeFeedRequestOptions>._)).Returns(iterator);
        using var document = JsonDocument.Parse("{\"id\":\"one\",\"_ts\":1700000000}");
        var pages = iterator.Pages;
        pages.Enqueue(Page(HttpStatusCode.OK, "empty"));
        pages.Enqueue(Page(HttpStatusCode.OK, "delivered", document));
        pages.Enqueue(Page(HttpStatusCode.NotModified, "caught-up"));
        await using var processor = Create(client, state);
        var delivered = 0;
        Task Handler(CosmosDbDataChangeEvent change, CancellationToken token)
        {
            delivered++;
            return failHandler ? Task.FromException(new InvalidOperationException("Handler failed")) : Task.CompletedTask;
        }
        if (failHandler)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessBatchAsync(Handler, CancellationToken.None));
            A.CallTo(() => state.SavePositionAsync(A<string>._, A<CosmosDbCdcPosition>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
        else
        {
            (await processor.ProcessBatchAsync(Handler, CancellationToken.None)).ShouldBe(1);
            A.CallTo(() => state.SavePositionAsync("poll", A<CosmosDbCdcPosition>.That.Matches(p => p.ContinuationToken == "delivered"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
        delivered.ShouldBe(1);
        iterator.Disposed.ShouldBeTrue();
    }

    private sealed class TrackingIterator : FeedIterator<JsonDocument>
    {
        public Queue<FeedResponse<JsonDocument>> Pages { get; } = new();
        public bool Disposed { get; private set; }
        public override bool HasMoreResults => true;
        public override Task<FeedResponse<JsonDocument>> ReadNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Pages.Dequeue());
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static FeedResponse<JsonDocument> Page(HttpStatusCode status, string token, params JsonDocument[] documents)
    {
        var page = A.Fake<FeedResponse<JsonDocument>>();
        A.CallTo(() => page.StatusCode).Returns(status);
        A.CallTo(() => page.Count).Returns(documents.Length);
        A.CallTo(() => page.ContinuationToken).Returns(token);
        A.CallTo(() => page.GetEnumerator()).ReturnsLazily(() => ((IEnumerable<JsonDocument>)documents).GetEnumerator());
        return page;
    }

    private static CosmosDbCdcProcessor Create(CosmosClient client, ICosmosDbCdcStateStore state) => new(client, state,
        Microsoft.Extensions.Options.Options.Create(new CosmosDbCdcOptions
        {
            ConnectionString = "AccountEndpoint=https://unused.documents.azure.com:443/;AccountKey=dGVzdA==;",
            ProcessorName = "poll", DatabaseId = "db", ContainerId = "source",
        }), NullLogger<CosmosDbCdcProcessor>.Instance);
}
