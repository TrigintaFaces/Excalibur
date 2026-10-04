// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Dispatch;
namespace Excalibur.Cdc.Tests.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class AutoMappingExecutionShould
{
    [Theory]
    [InlineData(false, MessageDisposition.Handled)]
    [InlineData(true, MessageDisposition.AcceptedForBackgroundExecution)]
    public async Task RejectUnfinishedDispatch(bool succeeded, MessageDisposition disposition)
    {
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.ServiceProvider).Returns(null!);
        var result = succeeded ? MessageResult.Success(null, null, null, disposition) : MessageResult.Failed("rejected");
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(result));
        var services = CreateServices(dispatcher);
        // Explicit singleton keeps this arm independent of the mapper lifetime defect.
        services.AddSingleton<Mapper>();
        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetServices<IDataChangeHandler>().Single();
        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(Change(), CancellationToken.None));
    }

    [Fact]
    public async Task OwnAndDisposeMapperScopeForEachChange()
    {
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.ServiceProvider).Returns(null!);
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(MessageResult.Success()));
        var services = CreateServices(dispatcher);
        var lifetimes = new List<MapperLease>();
        services.AddScoped(_ => { var lifetime = new MapperLease(); lifetimes.Add(lifetime); return lifetime; });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var handler = provider.GetServices<IDataChangeHandler>().Single();
        await handler.HandleAsync(Change(), CancellationToken.None);
        await handler.HandleAsync(Change(), CancellationToken.None);
        lifetimes.Count.ShouldBe(2);
        lifetimes.ShouldAllBe(x => x.Disposed);
    }

    private static ServiceCollection CreateServices(IDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dispatcher);
        services.AddScoped<MapperLease>();
        services.AddCdcProcessor(cdc => cdc.UseSqlServer(sql => sql
            .ConnectionString("Server=localhost;Database=Test;Encrypt=false").DatabaseName("Test"))
            .TrackTable("dbo.Items", table => table.MapInsert<Event, Mapper>()));
        return services;
    }
    private static DataChangeEvent Change() => new() { TableName = "dbo.Items", ChangeType = DataChangeType.Insert };
    internal sealed class Event : IDispatchEvent;
    internal sealed class Mapper(MapperLease lifetime) : ICdcEventMapper<Event>
    {
        public Event Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType)
        {
            lifetime.Disposed.ShouldBeFalse();
            return new Event();
        }
    }
    internal sealed class MapperLease : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
