using System.Data;

using Excalibur.DataAccess;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class IDbShould
{
	[Fact]
	public void ProvideAccessToUnderlyingConnection()
	{
		// Arrange
		var db = A.Fake<IDb>();
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => db.Connection).Returns(connection);

		// Act
		var result = db.Connection;

		// Assert
		result.ShouldBe(connection);
	}

	[Fact]
	public void SupportOpenMethod()
	{
		// Arrange
		var db = A.Fake<IDb>();

		// Act
		db.Open();

		// Assert
		_ = A.CallTo(() => db.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportCloseMethod()
	{
		// Arrange
		var db = A.Fake<IDb>();

		// Act
		db.Close();

		// Assert
		_ = A.CallTo(() => db.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var db = new TestDb(connection);

		// Assert
		_ = db.ShouldBeAssignableTo<IDisposable>();

		// Cleanup
		db.Dispose();
	}

	private sealed class TestDb(IDbConnection connection) : Db(connection);
}
