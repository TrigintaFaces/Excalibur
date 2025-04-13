using System.Data;

using Excalibur.DataAccess;
using Excalibur.DataAccess.DataProcessing;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataProcessorDbShould
{
	[Fact]
	public void ConstructorShouldThrowIfDbIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new DataProcessorDb(null!));
	}

	[Fact]
	public void ConnectionShouldReturnUnderlyingConnection()
	{
		// Arrange
		var expectedConnection = A.Fake<IDbConnection>();
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(expectedConnection);

		var processorDb = new DataProcessorDb(db);

		// Act & Assert
		processorDb.Connection.ShouldBe(expectedConnection);
	}

	[Fact]
	public void OpenShouldCallUnderlyingDbOpen()
	{
		// Arrange
		var db = A.Fake<IDb>();
		var processorDb = new DataProcessorDb(db);

		// Act
		processorDb.Open();

		// Assert
		_ = A.CallTo(() => db.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CloseShouldCallUnderlyingDbClose()
	{
		// Arrange
		var db = A.Fake<IDb>();
		var processorDb = new DataProcessorDb(db);

		// Act
		processorDb.Close();

		// Assert
		_ = A.CallTo(() => db.Close()).MustHaveHappenedOnceExactly();
	}
}
