using Company.ExcaliburCqrs.Application.Commands;
using Company.ExcaliburCqrs.Application.Queries;
using Company.ExcaliburCqrs.Domain.Aggregates;
using Company.ExcaliburCqrs.ReadModel;
using Excalibur.EventSourcing;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Company.ExcaliburCqrs.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ReadYourWritesShould
{
    [Fact]
    public async Task CarryTheCommittedReceiptIntoTheReadWithoutRepeatingTheCommand()
    {
        var repository = A.Fake<IEventSourcedRepository<Order, Guid>>();
        var receipt = new SaveResult(1, 42, ProjectionFreshness.NotRequested);
        Order? saved = null;
        A.CallTo(() => repository.SaveAsync(A<Order>._, A<CancellationToken>._))
            .Invokes((Order order, CancellationToken _) => saved = order)
            .Returns(receipt);
        var create = new CreateOrderCommandHandler(repository, NullLogger<CreateOrderCommandHandler>.Instance);
        var created = await create.HandleAsync(new CreateOrderCommand("product", 3), CancellationToken.None);
        created.OrderId.ShouldBe(saved.ShouldNotBeNull().Id);
        created.Session.ShouldBe(receipt);

        var sessionReader = A.Fake<IProjectionSessionReader>();
        var pending = new ProjectionRead<OrderReadModel>(null, ProjectionFreshness.BoundExpired);
        A.CallTo(() => sessionReader.ReadAsync<OrderReadModel>(created.OrderId.ToString(), receipt, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(pending));
        var query = new GetOrderQueryHandler(sessionReader, NullLogger<GetOrderQueryHandler>.Instance);
        var result = await query.HandleAsync(new GetOrderQuery(created.OrderId, created.Session), CancellationToken.None);
        result.Freshness.ShouldBe(ProjectionFreshness.BoundExpired);
        result.Projection.ShouldBeNull();
        A.CallTo(() => repository.SaveAsync(A<Order>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
