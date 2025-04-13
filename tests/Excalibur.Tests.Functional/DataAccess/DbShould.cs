using System.Data;

using Excalibur.DataAccess;

using FakeItEasy;
using FakeItEasy.Creation;

namespace Excalibur.Tests.Functional.DataAccess;

public class DbShould
{
	[Fact]
	public void OpenAndCloseConnectionSuccessfully()
	{
		// Arrange
		using var connection = A.Fake<IDbConnection>();
		var db = A.Fake<Db>((IFakeOptions<Db> x) => x.WithArgumentsForConstructor([connection]));

		// Act
		db.Open();
		db.Close();

		// Assert
		_ = A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeConnectionSuccessfully()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var db = A.Fake<Db>(
			(IFakeOptions<Db> options) => options.WithArgumentsForConstructor([connection])
				.CallsBaseMethods() // Allow calling base methods including Dispose
		);

		// Act
		db.Dispose();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}
}
