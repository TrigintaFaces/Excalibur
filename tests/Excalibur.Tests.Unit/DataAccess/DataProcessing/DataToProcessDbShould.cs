using System.Data;

using Excalibur.DataAccess;
using Excalibur.DataAccess.DataProcessing;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataToProcessDbShould
{
	private readonly IDb _fakeDb = A.Fake<IDb>();
	private readonly DataToProcessDb _dataToProcessDb;

	public DataToProcessDbShould()
	{
		_dataToProcessDb = new DataToProcessDb(_fakeDb);
	}

	[Fact]
	public void ConstructorShouldThrowIfDbIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new DataToProcessDb(null!));
	}

	[Fact]
	public void ConnectionShouldReturnDbConnection()
	{
		// Arrange
		var fakeConnection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => _fakeDb.Connection).Returns(fakeConnection);

		// Act
		var connection = _dataToProcessDb.Connection;

		// Assert
		connection.ShouldBe(fakeConnection);
		_ = A.CallTo(() => _fakeDb.Connection).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void OpenShouldCallDbOpen()
	{
		// Act
		_dataToProcessDb.Open();

		// Assert
		_ = A.CallTo(() => _fakeDb.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CloseShouldCallDbClose()
	{
		// Act
		_dataToProcessDb.Close();

		// Assert
		_ = A.CallTo(() => _fakeDb.Close()).MustHaveHappenedOnceExactly();
	}
}
