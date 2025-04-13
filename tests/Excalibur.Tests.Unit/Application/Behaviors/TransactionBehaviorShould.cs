using System.Transactions;

using Excalibur.Application.Behaviors;
using Excalibur.Application.Requests;
using Excalibur.Domain;

using FakeItEasy;

using MediatR;

using Shouldly;

namespace Excalibur.Tests.Unit.Application.Behaviors;

public class TransactionBehaviorShould
{
	[Fact]
	public async Task CallNextWithoutTransactionForNonTransactionalRequests()
	{
		// Arrange
		var db = A.Fake<IDomainDb>();
		var behavior = new TransactionBehavior<TestRequest, string>(db);
		var request = new TestRequest();
		var next = A.Fake<RequestHandlerDelegate<string>>();
		const string expected = "Success";

		_ = A.CallTo(() => next()).Returns(expected);

		// Act
		var result = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
		A.CallTo(() => db.Open()).MustNotHaveHappened();
		A.CallTo(() => db.Close()).MustNotHaveHappened();
	}

	[Fact]
	public async Task OpenAndCloseConnectionWhenHandlingTransactionalRequests()
	{
		// Arrange
		var db = A.Fake<IDomainDb>();
		var behavior = new TransactionBehavior<TransactionalRequest, string>(db);
		var request = new TransactionalRequest
		{
			TransactionTimeout = TimeSpan.FromSeconds(30),
			TransactionIsolation = IsolationLevel.ReadCommitted,
			TransactionBehavior = TransactionScopeOption.Required
		};
		var next = A.Fake<RequestHandlerDelegate<string>>();
		const string expected = "Success";

		_ = A.CallTo(() => next()).Returns(expected);

		// Act
		var result = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
		_ = A.CallTo(() => db.Open()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => db.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CloseConnectionEvenWhenExceptionIsThrown()
	{
		// Arrange
		var db = A.Fake<IDomainDb>();
		var behavior = new TransactionBehavior<TransactionalRequest, string>(db);
		var request = new TransactionalRequest();
		var next = A.Fake<RequestHandlerDelegate<string>>();
		var exception = new InvalidOperationException("Test exception");

		_ = A.CallTo(() => next()).Throws(exception);

		// Act & Assert
		var thrownException = await Should.ThrowAsync<InvalidOperationException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		// Verify exception is the original one
		thrownException.ShouldBeSameAs(exception);

		// Verify connection is opened and closed even when exception occurs
		_ = A.CallTo(() => db.Open()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => db.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenRequestIsNull()
	{
		// Arrange
		var db = A.Fake<IDomainDb>();
		var behavior = new TransactionBehavior<TransactionalRequest, string>(db);
		TransactionalRequest request = null!;
		var next = A.Fake<RequestHandlerDelegate<string>>();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("request");
		A.CallTo(() => db.Open()).MustNotHaveHappened();
		A.CallTo(() => db.Close()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenNextIsNull()
	{
		// Arrange
		var db = A.Fake<IDomainDb>();
		var behavior = new TransactionBehavior<TransactionalRequest, string>(db);
		var request = new TransactionalRequest();
		RequestHandlerDelegate<string> next = null!;

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("next");
		A.CallTo(() => db.Open()).MustNotHaveHappened();
		A.CallTo(() => db.Close()).MustNotHaveHappened();
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenDbIsNull()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new TransactionBehavior<TransactionalRequest, string>(null!));

		exception.ParamName.ShouldBe("db");
	}

	// Test request classes
	private sealed class TestRequest : IRequest<string>
	{
	}

	private sealed class TransactionalRequest : IRequest<string>, IAmTransactional
	{
		public TransactionScopeOption TransactionBehavior { get; set; } = TransactionScopeOption.Required;
		public TimeSpan TransactionTimeout { get; set; } = TimeSpan.FromSeconds(60);
		public IsolationLevel TransactionIsolation { get; set; } = IsolationLevel.ReadCommitted;
	}
}
