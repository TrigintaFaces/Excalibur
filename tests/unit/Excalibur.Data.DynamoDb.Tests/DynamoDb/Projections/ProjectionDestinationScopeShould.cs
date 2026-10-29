// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Endpoint = Amazon.Runtime.Endpoints.Endpoint;
using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionDestinationScopeShould
{
    private const string Arn = "arn:aws:dynamodb:us-east-1:123456789012:table/read-models";

    [Fact]
    public async Task OpenAnExactGenerationConfiguredByTableArn()
    {
        var client = Client();
        A.CallTo(() => client.DescribeTableAsync(Arn, A<CancellationToken>._))
            .Returns(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn } });
        IProjectionStore<Row> store = new DynamoDbProjectionStore<Row>(client,
            new SingleNamedProjectionOptionsMonitor(new DynamoDbProjectionStoreOptions { TableName = Arn, AutoCreateTable = false }),
            NullLogger<DynamoDbProjectionStore<Row>>.Instance);
        var identity = ((IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        identity.ShouldContain(Uri.EscapeDataString(Arn));
        var factory = (IProjectionGenerationReaderFactory<Row>)store.GetService(typeof(IProjectionGenerationReaderFactory<Row>))!;
        await using var reader = factory.OpenReader(identity);
        _ = await reader.GetByIdAsync("id", CancellationToken.None);
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == Arn), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PinRequestsToTheDiscoveredAccountAndReuseOnlySuccessfulResolution()
    {
        var client = Client();
        var store = Store(client);
        var scope = (IAsyncProjectionDestinationScope)((IServiceProvider)store).GetService(typeof(IAsyncProjectionDestinationScope))!;
        var resolved = await scope.GetScopeAsync(CancellationToken.None);
        resolved.ShouldContain("123456789012");
        resolved.ShouldContain("127.0.0.1");
        // The same client now resolves a different account; existing store requests remain pinned.
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .Returns(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn.Replace("123456789012", "999999999999", StringComparison.Ordinal) } });
        (await scope.GetScopeAsync(CancellationToken.None)).ShouldBe(resolved);
        _ = await store.GetByIdAsync("id", CancellationToken.None);
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == Arn), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterMetadataFailureOrCancellation(bool canceled)
    {
        var client = Client();
        var scope = (IAsyncProjectionDestinationScope)((IServiceProvider)Store(client)).GetService(typeof(IAsyncProjectionDestinationScope))!;
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .ThrowsAsync(canceled ? new OperationCanceledException() : new InvalidOperationException("temporary metadata failure"));
        await Should.ThrowAsync<Exception>(async () => await scope.GetScopeAsync(CancellationToken.None));
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .Returns(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn } });
        (await scope.GetScopeAsync(CancellationToken.None)).ShouldContain("123456789012");
    }

    [Fact]
    public async Task ResolveGenerationHandleWithoutCreatingItsTargetTable()
    {
        var client = Client();
        var store = Store(client);
        var factory = (IProjectionGenerationReaderFactory<Row>)((IServiceProvider)store).GetService(typeof(IProjectionGenerationReaderFactory<Row>))!;
        await using var reader = factory.OpenReader("dynamodb/read-models/Example.Row");
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldNotBeNull();
        _ = await reader.GetByIdAsync("id", CancellationToken.None);
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == Arn), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => client.CreateTableAsync(A<CreateTableRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReuseAuthoritativeMetadataAcrossShortLivedGenerationHandles()
    {
        var client = Client();
        var store = Store(client);
        var factory = (IProjectionGenerationReaderFactory<Row>)((IServiceProvider)store).GetService(typeof(IProjectionGenerationReaderFactory<Row>))!;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await using var reader = factory.OpenReader("dynamodb/read-models/Example.Row");
            _ = await reader.GetByIdAsync("id", CancellationToken.None);
            (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldContain("123456789012");
        }
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == Arn), A<CancellationToken>._)).MustHaveHappened(10, Times.Exactly);
    }

    [Fact]
    public async Task KeepEachHandlePinnedWhenEvictionAndCredentialChangesRace()
    {
        var client = Client();
        A.CallTo(() => client.DescribeTableAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string table, CancellationToken _) => new DescribeTableResponse
            {
                Table = new TableDescription { TableArn = "arn:aws:dynamodb:us-east-1:123456789012:table/" + table },
            });
        var store = Store(client);
        var factory = (IProjectionGenerationReaderFactory<Row>)((IServiceProvider)store).GetService(typeof(IProjectionGenerationReaderFactory<Row>))!;
        await using var original = factory.OpenReader("dynamodb/read-models/Example.Row");
        (await original.GetDestinationScopeAsync(CancellationToken.None)).ShouldContain("123456789012");
        for (var index = 0; index < 128; index++)
        {
            await using var other = factory.OpenReader($"dynamodb/other-{index}/Example.Row");
            _ = await other.GetDestinationScopeAsync(CancellationToken.None);
        }
        var firstResponse = new TaskCompletionSource<DescribeTableResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResponse = new TaskCompletionSource<DescribeTableResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .ReturnsLazily(() => Interlocked.Increment(ref calls) == 1 ? firstResponse.Task : secondResponse.Task);
        await using var first = factory.OpenReader("dynamodb/read-models/Example.Row");
        await using var second = factory.OpenReader("dynamodb/read-models/Example.Row");
        var firstScope = first.GetDestinationScopeAsync(CancellationToken.None).AsTask();
        var secondScope = second.GetDestinationScopeAsync(CancellationToken.None).AsTask();
        calls.ShouldBe(2);
        var changedArn = Arn.Replace("123456789012", "999999999999", StringComparison.Ordinal);
        secondResponse.SetResult(new DescribeTableResponse { Table = new TableDescription { TableArn = changedArn } });
        (await secondScope).ShouldContain("999999999999");
        firstResponse.SetResult(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn } });
        (await firstScope).ShouldBe(await secondScope);
        _ = await first.GetByIdAsync("first", CancellationToken.None);
        _ = await second.GetByIdAsync("second", CancellationToken.None);
        _ = await original.GetByIdAsync("original", CancellationToken.None);
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == changedArn), A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == Arn), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>.That.Matches(r => r.TableName == "read-models"), A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static IAmazonDynamoDB Client()
    {
        var client = A.Fake<IAmazonDynamoDB>();
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .Returns(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn } });
        A.CallTo(() => client.DetermineServiceOperationEndpoint(A<AmazonWebServiceRequest>._)).Returns(new Endpoint("http://127.0.0.1:8000"));
        A.CallTo(() => client.GetItemAsync(A<GetItemRequest>._, A<CancellationToken>._)).Returns(new GetItemResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });
        return client;
    }

    [Fact]
    public async Task ProvisionBeforeResolvingScopeAndRetryTransientPostCreateMetadataAbsence()
    {
        var client = Client();
        var descriptions = 0;
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .ReturnsLazily(() => ++descriptions <= 2
                ? Task.FromException<DescribeTableResponse>(new Amazon.DynamoDBv2.Model.ResourceNotFoundException("not visible yet"))
                : Task.FromResult(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn } }));
        var store = new DynamoDbProjectionStore<Row>(client,
            new SingleNamedProjectionOptionsMonitor(new DynamoDbProjectionStoreOptions { TableName = "read-models", AutoCreateTable = true }),
            NullLogger<DynamoDbProjectionStore<Row>>.Instance);
        var scope = (IAsyncProjectionDestinationScope)((IServiceProvider)store).GetService(typeof(IAsyncProjectionDestinationScope))!;
        (await scope.GetScopeAsync(CancellationToken.None)).ShouldContain("123456789012");
        descriptions.ShouldBe(3);
        A.CallTo(() => client.CreateTableAsync(A<CreateTableRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectMetadataForADifferentTable()
    {
        var client = Client();
        A.CallTo(() => client.DescribeTableAsync("read-models", A<CancellationToken>._))
            .Returns(new DescribeTableResponse { Table = new TableDescription { TableArn = Arn + "-other" } });
        var scope = (IAsyncProjectionDestinationScope)((IServiceProvider)Store(client)).GetService(typeof(IAsyncProjectionDestinationScope))!;
        await Should.ThrowAsync<InvalidOperationException>(async () => await scope.GetScopeAsync(CancellationToken.None));
    }

    private static DynamoDbProjectionStore<Row> Store(IAmazonDynamoDB client) => new(client,
        new SingleNamedProjectionOptionsMonitor(new DynamoDbProjectionStoreOptions { TableName = "read-models", AutoCreateTable = false }),
        NullLogger<DynamoDbProjectionStore<Row>>.Instance);

    internal sealed class Row { public string Id { get; set; } = string.Empty; }
}
