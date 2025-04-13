using System.Data;

using Excalibur.Domain;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class DomainDbShould
{
	[Fact]
	public void ImplementIDomainDbInterface()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act
		using var domainDb = new DomainDb(connection);

		// Assert
		_ = domainDb.ShouldBeAssignableTo<IDomainDb>();
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Arrange
		IDbConnection connection = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => new DomainDb(connection));
		exception.ParamName.ShouldBe("connection");
	}

	[Fact]
	public void ReturnSameConnectionInstanceThatWasPassedInConstructor()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		using var domainDb = new DomainDb(connection);

		// Assert
		_ = domainDb.Connection.ShouldNotBeNull();
	}

	[Fact]
	public void OpenConnectionWhenOpenIsCalled()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);
		using var domainDb = new DomainDb(connection);

		// Act
		domainDb.Open();

		// Assert
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CloseConnectionWhenCloseIsCalled()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var domainDb = new DomainDb(connection);

		// Act
		domainDb.Close();

		// Assert
		_ = A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeConnectionWhenDisposed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var domainDb = new DomainDb(connection);

		// Act
		domainDb.Dispose();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void NotDisposeConnectionTwice()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var domainDb = new DomainDb(connection);

		// Act
		domainDb.Dispose();
		domainDb.Dispose(); // Call dispose a second time

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}
}
