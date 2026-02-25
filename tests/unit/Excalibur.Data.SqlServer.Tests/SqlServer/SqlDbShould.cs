using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlDbShould
{
    [Fact]
    public void InheritFromDb()
    {
        // Arrange
        var connection = A.Fake<IDbConnection>();

        // Act
        var sqlDb = new SqlDb(connection);

        // Assert
        sqlDb.ShouldBeAssignableTo<Db>();
    }

    [Fact]
    public void ExposeConnection()
    {
        // Arrange
        var connection = A.Fake<IDbConnection>();

        // Act
        var sqlDb = new SqlDb(connection);

        // Assert
        sqlDb.Connection.ShouldBeSameAs(connection);
    }

    [Fact]
    public void DisposeConnection()
    {
        // Arrange
        var connection = A.Fake<IDbConnection>();
        var sqlDb = new SqlDb(connection);

        // Act
        sqlDb.Dispose();

        // Assert
        A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
    }
}
