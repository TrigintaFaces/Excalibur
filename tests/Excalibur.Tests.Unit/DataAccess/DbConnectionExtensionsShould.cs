using System.Data;

using Excalibur.DataAccess;
using Excalibur.DataAccess.Exceptions;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DbConnectionExtensionsShould
{
	[Fact]
	public async Task ResolveAsyncWhenRequestSucceedsReturnExpectedResult()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		var expectedResult = "Test Result";

		_ = A.CallTo(() => dataRequest.ResolveAsync(connection)).Returns(expectedResult);

		// Act
		var result = await connection.ResolveAsync(dataRequest).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => dataRequest.ResolveAsync(connection)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ResolveAsyncWhenRequestFailsThrowOperationFailedException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidOperationException("Test exception");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		_ = A.CallTo(() => dataRequest.ResolveAsync(connection)).Throws(exception);

		// Act & Assert
		var thrownException = await Should.ThrowAsync<OperationFailedException>(
			async () => await connection.ResolveAsync(dataRequest).ConfigureAwait(true)).ConfigureAwait(true);

		thrownException.ShouldNotBeNull();
		thrownException.Message.ShouldBe("The operation failed.");
		thrownException.InnerException.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task ResolveAsyncWhenApiExceptionOccursPropagateException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var apiException = new ResourceNotFoundException("resourceKey", "TestResource", message: "Test resource not found");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		_ = A.CallTo(() => dataRequest.ResolveAsync(connection)).Throws(apiException);

		// Act & Assert
		var thrownException = await Should.ThrowAsync<ResourceNotFoundException>(
			async () => await connection.ResolveAsync(dataRequest).ConfigureAwait(true)).ConfigureAwait(true);

		thrownException.ShouldBeSameAs(apiException);
	}

	[Fact]
	public async Task ResolveAsyncWhenNullRequestThrowArgumentNullException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		IDataRequest<IDbConnection, string> dataRequest = null;

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await connection.ResolveAsync(dataRequest).ConfigureAwait(true)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("dataRequest");
	}

	[Fact]
	public void ReadyWhenConnectionIsClosedOpenConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReadyWhenConnectionIsBrokenCloseAndOpenConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		_ = A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReadyWhenConnectionIsAlreadyOpenReturnSameConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
		A.CallTo(() => connection.Close()).MustNotHaveHappened();
	}

	[Fact]
	public void ReadyWhenConnectionIsDisposedThrowInvalidOperationException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Configure Open to throw ObjectDisposedException
		_ = A.CallTo(() => connection.Open()).Throws(new ObjectDisposedException("connection"));

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => connection.Ready());

		exception.Message.ShouldContain("database connection has been disposed");
	}

	[Fact]
	public void ReadyWhenConnectionIsNullThrowArgumentNullException()
	{
		// Arrange
		IDbConnection connection = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => connection.Ready());

		exception.ParamName.ShouldBe("connection");
	}
}
