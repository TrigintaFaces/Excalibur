using System.Data;

using Excalibur.DataAccess;
using Excalibur.DataAccess.Exceptions;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Functional.DataAccess;

public class DbConnectionExtensionsShould
{
	[Fact]
	public async Task ThrowOperationFailedExceptionWhenQueryFails()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();

		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		_ = A.CallTo(() => dataRequest.ResolveAsync(connection)).Throws<InvalidOperationException>();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationFailedException>(() => connection.ResolveAsync(dataRequest)).ConfigureAwait(false);
	}

	[Fact]
	public void ReopenConnectionIfBroken()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);

		// Act
		var result = connection.Ready();

		// Assert
		_ = A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void OpenConnectionIfClosed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Act
		var result = connection.Ready();

		// Assert
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}
}
