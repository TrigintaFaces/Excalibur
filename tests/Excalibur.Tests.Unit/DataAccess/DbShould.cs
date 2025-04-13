using System.Data;

using Excalibur.DataAccess;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DbShould
{
	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TestDb(null!));
	}

	[Fact]
	public void OpenShouldCallOpenOnConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);
		using var db = new TestDb(connection);

		// Act
		db.Open();

		// Assert
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CloseShouldCallCloseOnConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		using var db = new TestDb(connection);

		// Act
		db.Close();

		// Assert
		_ = A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeShouldCallDisposeOnConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var db = new TestDb(connection);

		// Act
		db.Dispose();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeShouldBeIdempotent()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var db = new TestDb(connection);

		// Act
		db.Dispose();
		db.Dispose();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	private sealed class TestDb(IDbConnection connection) : Db(connection);
}
