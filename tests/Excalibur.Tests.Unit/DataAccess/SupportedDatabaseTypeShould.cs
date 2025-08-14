using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class SupportedDatabaseTypeShould
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Arrange & Act & Assert
		((int)SupportedDatabase.Unknown).ShouldBe(0);
		((int)SupportedDatabase.Postgres).ShouldBe(1);
		((int)SupportedDatabase.SqlServer).ShouldBe(2);
	}

	[Fact]
	public void BeUsableInSwitchExpressions()
	{
		// Arrange
		var database = SupportedDatabase.SqlServer;

		// Act
		var result = database switch
		{
			SupportedDatabase.Unknown => "Unknown",
			SupportedDatabase.Postgres => "PostgreSQL",
			SupportedDatabase.SqlServer => "Microsoft SQL Server",
			_ => throw new ArgumentOutOfRangeException(nameof(database))
		};

		// Assert
		result.ShouldBe("Microsoft SQL Server");
	}

	[Theory]
	[InlineData(SupportedDatabase.Unknown, false)]
	[InlineData(SupportedDatabase.Postgres, true)]
	[InlineData(SupportedDatabase.SqlServer, true)]
	public void IdentifyKnownDatabaseTypes(SupportedDatabase database, bool expected)
	{
		// Arrange & Act
		var isKnownType = database != SupportedDatabase.Unknown;

		// Assert
		isKnownType.ShouldBe(expected);
	}

	[Fact]
	public void SupportEnumTryParse()
	{
		// Arrange
		var databaseText = "SqlServer";

		// Act
		var success = Enum.TryParse<SupportedDatabase>(databaseText, out var database);

		// Assert
		success.ShouldBeTrue();
		database.ShouldBe(SupportedDatabase.SqlServer);
	}
}
