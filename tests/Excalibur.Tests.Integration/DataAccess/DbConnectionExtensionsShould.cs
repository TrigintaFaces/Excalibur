using System.Data;
using System.Data.Common;

using Excalibur.DataAccess;
using Excalibur.DataAccess.Exceptions;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Integration.DataAccess;

public class DbConnectionExtensionsShould
{
	[Fact]
	public void ReadyShouldOpenConnectionIfClosed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Act
		_ = connection.Ready();

		// Assert
		_ = A.CallTo(() => connection.Open()).MustHaveHappened();
	}

	[Fact]
	public void ReadyShouldCloseAndOpenConnectionIfBroken()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);

		// Act
		_ = connection.Ready();

		// Assert
		_ = A.CallTo(() => connection.Close()).MustHaveHappened();
		_ = A.CallTo(() => connection.Open()).MustHaveHappened();
	}

	[Fact]
	public void ReadyShouldReturnConnectionIfAlreadyOpen()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBe(connection);
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
	}

	[Fact]
	public void ReadyShouldThrowIfDisposed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);
		_ = A.CallTo(() => connection.Open()).Throws(new ObjectDisposedException("connection"));

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => connection.Ready())
			.Message.ShouldContain("has been disposed");
	}

	[Fact]
	public async Task ResolveAsyncShouldReturnResultFromResolver()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var expected = "resolved";
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		_ = A.CallTo(() => request.ResolveAsync(connection)).Returns(Task.FromResult(expected));

		// Act
		var result = await connection.ResolveAsync(request).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task ResolveAsyncShouldWrapExceptionAsOperationFailed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		_ = A.CallTo(() => request.ResolveAsync(connection)).Throws(new TestDbException("boom"));

		// Act & Assert
		var ex = await Should.ThrowAsync<OperationFailedException>(async () => await connection.ResolveAsync(request).ConfigureAwait(true))
			.ConfigureAwait(true);
		_ = ex.InnerException.ShouldBeOfType<TestDbException>();
		ex.Message.ShouldContain("The operation failed");
	}

	[Fact]
	public async Task ResolveAsyncShouldNotWrapIfAlreadyApiException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		_ = A.CallTo(() => request.ResolveAsync(connection))
			.Throws(new OperationFailedException("op", "res"));

		// Act & Assert
		_ = await Should.ThrowAsync<OperationFailedException>(async () => await connection.ResolveAsync(request).ConfigureAwait(true))
			.ConfigureAwait(true);
	}

	private sealed class TestDbException(string message) : DbException(message);
}
