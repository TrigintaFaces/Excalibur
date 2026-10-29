// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionGenerationReaderShould
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RejectAProviderReaderReusedFromAnotherTenantBeforeOpeningAConnection(bool postgres, bool generated)
    {
        var currentTenant = "Acme";
        var tenant = A.Fake<Excalibur.Dispatch.ITenantContext>();
        A.CallTo(() => tenant.TenantId).ReturnsLazily(() => currentTenant);
        A.CallTo(() => tenant.HasTenant).Returns(true);
        await using var dataSource = Npgsql.NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1");
        IProjectionStore<GenerationReaderProbe> store = postgres
            ? new Excalibur.EventSourcing.Postgres.PostgresProjectionStore<GenerationReaderProbe>(dataSource,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Excalibur.EventSourcing.Postgres.PostgresProjectionStore<GenerationReaderProbe>>.Instance, tenant)
            : new Excalibur.EventSourcing.SqlServer.SqlServerProjectionStore<GenerationReaderProbe>(
                "Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;Connect Timeout=1",
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Excalibur.EventSourcing.SqlServer.SqlServerProjectionStore<GenerationReaderProbe>>.Instance, tenant);
        var destination = postgres ? "postgres/public/rows" : "sqlserver/dbo/rows";
        await using var reader = generated
            ? store.GetService(typeof(IAotProjectionGenerationReaderFactory<GenerationReaderProbe>))
                .ShouldBeAssignableTo<IAotProjectionGenerationReaderFactory<GenerationReaderProbe>>()
                .OpenReader(destination, GenerationReaderJsonContext.Default.GenerationReaderProbe)
            : store.GetService(typeof(IProjectionGenerationReaderFactory<GenerationReaderProbe>))
                .ShouldBeAssignableTo<IProjectionGenerationReaderFactory<GenerationReaderProbe>>().OpenReader(destination);
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldNotBeNull();
        currentTenant = "acme";
        Should.Throw<InvalidOperationException>(() => reader.DestinationScope);
        await Should.ThrowAsync<InvalidOperationException>(async () => await reader.GetDestinationScopeAsync(CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => reader.QueryAsync(null, null, CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => reader.CountAsync(null, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PairReaderAndControlWithTheSelectedSource(bool inline)
    {
        var checkpoints = new InMemorySubscriptionCheckpointStore();
        var subscription = inline ? "Inline:sub" : "sub";
        var member = (await checkpoints.BindSubscriptionAsync(subscription,
            [new SubscriptionMember("A", new string('A', 64), "test/A") { DestinationScope = "estate/test/cluster" }], CancellationToken.None)).Members.Single();
        _ = await checkpoints.AdvanceCheckpointAsync(subscription, "A", member.BindingId, null, 10, CancellationToken.None);
        var query = A.Fake<IGlobalStreamQuery>();
        A.CallTo(() => query.GetHeadPositionAsync(A<CancellationToken>._)).Returns(new ValueTask<long>(10));
        var factory = new ReaderFactory();
        var store = A.Fake<IProjectionStore<GenerationReaderProbe>>();
        A.CallTo(() => store.GetService(typeof(IAotProjectionGenerationReaderFactory<GenerationReaderProbe>))).Returns(factory);
        A.CallTo(() => store.GetService(typeof(IProjectionTenantBoundary))).Returns(ProjectionTenantBoundary.EstateWide);
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton(query);
        services.AddSingleton<ISubscriptionCheckpointStore>(inline ? new InMemorySubscriptionCheckpointStore() : checkpoints);
        if (inline)
        {
            services.AddSingleton(new InlineProjectionSource("test/source", checkpoints, query));
            services.AddSingleton(Options.Create(new GlobalStreamProjectionOptions { ProjectionName = "sub" }));
            services.AddInlineProjectionGenerationReader<GenerationReaderProbe>("reader", ["A"]);
        }
        else
        {
            services.AddProjectionGenerationReader<GenerationReaderProbe>(subscription, "reader", ["A"]);
        }
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var control = scope.ServiceProvider.GetRequiredService<ProjectionGenerationControl<GenerationReaderProbe>>();
        (await control.ReadGenerationAsync(CancellationToken.None)).ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => control.CutOverAsync("unsupported", null, 0, CancellationToken.None));
        (await control.ReadGenerationAsync(CancellationToken.None)).ShouldBeNull();
        (await control.CutOverAsync("A", null, 0, CancellationToken.None)).Outcome.ShouldBe(ProjectionCutoverOutcome.CutOver);
        (await control.ReadGenerationAsync(CancellationToken.None)).ShouldNotBeNull().Member.BindingId.ShouldBe(member.BindingId);
        var reader = scope.ServiceProvider.GetRequiredService<ProjectionGenerationReader<GenerationReaderProbe>>();
        (await reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None))!
            .Destination.ShouldBe("test/A");
        var active = (await control.ReadGenerationAsync(CancellationToken.None))!;
        (await control.DecommissionAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeFalse();
        (await control.DecommissionAsync(active.Revision, CancellationToken.None)).ShouldBeTrue();
        (await control.ReadGenerationAsync(CancellationToken.None)).ShouldBeNull();
        (await checkpoints.ReadSubscriptionAsync(subscription, CancellationToken.None)).Members.Single().BindingId.ShouldBe(member.BindingId);
    }

    [Fact]
    public void RejectAmbiguousMixedSourceRegistrationsForTheSameProjectionType()
    {
        var services = new ServiceCollection();
        services.AddProjectionGenerationReader<GenerationReaderProbe>("sub", "reader");
        Should.Throw<InvalidOperationException>(() => services.AddInlineProjectionGenerationReader<GenerationReaderProbe>("reader"));
    }

    [Fact]
    public async Task RefuseCutoverToUnknownStorageScopeWithoutReplacingTheActivePointer()
    {
        var (checkpoints, _, _) = await ArrangeAsync();
        var old = (await checkpoints.ReadGenerationAsync("sub", "reader", CancellationToken.None))!;
        var roster = await checkpoints.BindSubscriptionAsync("sub", [new SubscriptionMember("C", new string('C', 64), "test/C")], CancellationToken.None);
        var member = roster.Members.Single(m => m.ProjectionKey == "C");
        _ = await checkpoints.AdvanceCheckpointAsync("sub", "C", member.BindingId, null, 10, CancellationToken.None);
        var cutover = new ProjectionGenerationCutover(checkpoints, A.Fake<IGlobalStreamQuery>());
        await Should.ThrowAsync<InvalidOperationException>(() => cutover.CutOverAsync(new ProjectionGenerationCutoverRequest
        {
            SubscriptionName = "sub", ReaderName = "reader", NewProjectionKey = "C", ExpectedRevision = old.Revision, MaxAcceptableLag = 0,
        }, CancellationToken.None));
        (await checkpoints.ReadGenerationAsync("sub", "reader", CancellationToken.None))!.Revision.ShouldBe(old.Revision);
    }

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RetryWhenThePointerChangesDuringReadIncludingRollback(bool rollback)
	{
		var (checkpoints, a, b) = await ArrangeAsync();
		var factory = new ReaderFactory();
		factory.OnRead = async () =>
		{
			if (factory.Opens != 1) { return; }
			var current = (await checkpoints.ReadGenerationAsync("sub", "reader", CancellationToken.None))!;
			(await checkpoints.TrySetGenerationAsync("sub", "reader", current.Revision, "B", b.BindingId, 10, CancellationToken.None)).ShouldBeTrue();
			if (rollback)
			{
				current = (await checkpoints.ReadGenerationAsync("sub", "reader", CancellationToken.None))!;
				(await checkpoints.TrySetGenerationAsync("sub", "reader", current.Revision, "A", a.BindingId, 10, CancellationToken.None)).ShouldBeTrue();
			}
		};
		var reader = CreateReader(checkpoints, factory, ["A", "B"]);
		var result = await reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None);
		result.ShouldNotBeNull().Destination.ShouldBe(rollback ? "test/A" : "test/B");
		factory.Opens.ShouldBe(2);
		factory.Disposals.ShouldBe(2);
	}

	[Fact]
	public async Task RefuseAnUnsupportedSchemaBeforeReading()
	{
		var (checkpoints, _, _) = await ArrangeAsync();
		var factory = new ReaderFactory();
		var reader = CreateReader(checkpoints, factory, ["B"]);
		var error = await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
		error.Message.ShouldContain("does not support");
		factory.Opens.ShouldBe(0);
	}

	[Fact]
	public async Task RefuseMissingRoutingInsteadOfReadingTheWriterDestination()
	{
		var factory = new ReaderFactory();
		var reader = CreateReader(new InMemorySubscriptionCheckpointStore(), factory, ["A"]);
		await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
		factory.Opens.ShouldBe(0);
	}

	[Fact]
	public async Task RefuseGeneratedReadsWithoutFallingBackToTheConventionalCodec()
	{
		var (checkpoints, _, _) = await ArrangeAsync();
		var store = A.Fake<IProjectionStore<GenerationReaderProbe>>();
		A.CallTo(() => store.GetService(typeof(IProjectionTenantBoundary))).Returns(ProjectionTenantBoundary.EstateWide);
		var reader = new ProjectionGenerationReader<GenerationReaderProbe>(checkpoints, store, "sub", "reader", ["A"]);
		await Should.ThrowAsync<NotSupportedException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
		A.CallTo(() => store.GetByIdAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RouteQueriesAndCountsThroughTheSameValidatedLifetime()
	{
		var (checkpoints, _, _) = await ArrangeAsync();
		var factory = new ReaderFactory();
		var reader = CreateReader(checkpoints, factory, ["A"]);
		(await reader.QueryAsync(null, null, GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None)).Single().Destination.ShouldBe("test/A");
		(await reader.CountAsync(null, GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None)).ShouldBe(1);
		factory.Disposals.ShouldBe(2);
	}

    [Fact]
    public async Task BoundRetriesAndDisposeEveryHandleDuringContinuousSwitching()
    {
        var (checkpoints, a, _) = await ArrangeAsync();
        var factory = new ReaderFactory();
        factory.OnRead = async () =>
        {
            var current = (await checkpoints.ReadGenerationAsync("sub", "reader", CancellationToken.None))!;
            (await checkpoints.TrySetGenerationAsync("sub", "reader", current.Revision, "A", a.BindingId, 10, CancellationToken.None)).ShouldBeTrue();
        };
        var reader = CreateReader(checkpoints, factory, ["A"]);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
        error.Message.ShouldContain("three consecutive reads");
        factory.Opens.ShouldBe(3);
        factory.Disposals.ShouldBe(3);
    }

    [Fact]
    public async Task DisposeTheHandleWhenTheProviderThrows()
    {
        var (checkpoints, _, _) = await ArrangeAsync();
        var factory = new ReaderFactory { OnRead = () => throw new InvalidOperationException("provider failed") };
        var reader = CreateReader(checkpoints, factory, ["A"]);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
        error.Message.ShouldBe("provider failed");
        factory.Opens.ShouldBe(1);
        factory.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task HonorCancellationBeforeResolvingOrOpeningAHandle()
    {
        var (checkpoints, _, _) = await ArrangeAsync();
        var factory = new ReaderFactory();
        var reader = CreateReader(checkpoints, factory, ["A"]);
        await Should.ThrowAsync<OperationCanceledException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, new CancellationToken(true)));
        factory.Opens.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("test/different-cluster")]
    public async Task RefuseUnknownOrDifferentPhysicalScopeBeforeReading(string? scope)
    {
        var (checkpoints, _, _) = await ArrangeAsync();
        var factory = new ReaderFactory { Scope = scope };
        var reads = 0;
        factory.OnRead = () => { reads++; return Task.CompletedTask; };
        var reader = CreateReader(checkpoints, factory, ["A"]);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => reader.GetByIdAsync("id", GenerationReaderJsonContext.Default.GenerationReaderProbe, CancellationToken.None));
        error.Message.ShouldContain("storage scope");
        reads.ShouldBe(0);
        factory.Disposals.ShouldBe(1);
    }

	private static ProjectionGenerationReader<GenerationReaderProbe> CreateReader(InMemorySubscriptionCheckpointStore checkpoints, ReaderFactory factory, string[] keys)
	{
		var store = A.Fake<IProjectionStore<GenerationReaderProbe>>();
		A.CallTo(() => store.GetService(typeof(IAotProjectionGenerationReaderFactory<GenerationReaderProbe>))).Returns(factory);
        A.CallTo(() => store.GetService(typeof(IProjectionTenantBoundary))).Returns(ProjectionTenantBoundary.EstateWide);
		return new(checkpoints, store, "sub", "reader", keys);
	}

	private static async Task<(InMemorySubscriptionCheckpointStore Store, SubscriptionMember A, SubscriptionMember B)> ArrangeAsync()
	{
		var store = new InMemorySubscriptionCheckpointStore();
		var roster = await store.BindSubscriptionAsync("sub", [new("A", new string('A', 64), "test/A") { DestinationScope = "estate/test/cluster" }, new("B", new string('B', 64), "test/B") { DestinationScope = "estate/test/cluster" }], CancellationToken.None);
		foreach (var member in roster.Members)
		{
			_ = await store.AdvanceCheckpointAsync("sub", member.ProjectionKey, member.BindingId, null, 10, CancellationToken.None);
		}
		var a = roster.Members.Single(m => m.ProjectionKey == "A");
		_ = await store.TrySetGenerationAsync("sub", "reader", null, "A", a.BindingId, 10, CancellationToken.None);
		return (store, a, roster.Members.Single(m => m.ProjectionKey == "B"));
	}

	private sealed class ReaderFactory : IAotProjectionGenerationReaderFactory<GenerationReaderProbe>
	{
		public string? Scope { get; set; } = "test/cluster";
        public int Opens { get; private set; }
		public int Disposals { get; set; }
		public Func<Task>? OnRead { get; set; }
		public IProjectionGenerationReader<GenerationReaderProbe> OpenReader(string destinationIdentity, JsonTypeInfo<GenerationReaderProbe> typeInfo)
		{
			Opens++;
			return new Handle(this, destinationIdentity);
		}
		private sealed class Handle(ReaderFactory owner, string destination) : IProjectionGenerationReader<GenerationReaderProbe>
		{
			public string? DestinationScope => owner.Scope;
            public async Task<GenerationReaderProbe?> GetByIdAsync(string id, CancellationToken cancellationToken)
			{
				if (owner.OnRead is not null) { await owner.OnRead(); }
				return new(destination);
			}
			public Task<IReadOnlyList<GenerationReaderProbe>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
				Task.FromResult<IReadOnlyList<GenerationReaderProbe>>([new(destination)]);
			public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => Task.FromResult(1L);
			public ValueTask DisposeAsync() { owner.Disposals++; return ValueTask.CompletedTask; }
		}
	}
}

public sealed record GenerationReaderProbe(string Destination);

[JsonSerializable(typeof(GenerationReaderProbe))]
internal sealed partial class GenerationReaderJsonContext : JsonSerializerContext;
