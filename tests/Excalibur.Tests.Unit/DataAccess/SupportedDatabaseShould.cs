using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class SupportedDatabaseShould
{
	[Fact]
	public void HaveCorrectValues()
	{
		// Arrange & Act & Assert
		((int)SupportedDatabase.Unknown).ShouldBe(0);
		((int)SupportedDatabase.Postgres).ShouldBe(1);
		((int)SupportedDatabase.SqlServer).ShouldBe(2);
	}

	[Fact]
	public void HaveCorrectNumberOfValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<SupportedDatabase>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectNames()
	{
		// Arrange & Act
		var names = Enum.GetNames<SupportedDatabase>();

		// Assert
		names.ShouldContain("Unknown");
		names.ShouldContain("Postgres");
		names.ShouldContain("SqlServer");
	}

	[Fact]
	public void BeConvertibleBetweenIntAndEnum()
	{
		// Arrange & Act & Assert
		SupportedDatabase.Unknown.ShouldBe((SupportedDatabase)0);
		SupportedDatabase.Postgres.ShouldBe((SupportedDatabase)1);
		SupportedDatabase.SqlServer.ShouldBe((SupportedDatabase)2);
	}

	[Fact]
	public void BeUsableInSwitchStatements()
	{
		// Arrange
		var database = SupportedDatabase.Postgres;
		var result = string.Empty;

		// Act
		switch (database)
		{
			case SupportedDatabase.Unknown:
				result = "Unknown";
				break;

			case SupportedDatabase.Postgres:
				result = "Postgres";
				break;

			case SupportedDatabase.SqlServer:
				result = "SqlServer";
				break;

			default:
				result = "Unexpected";
				break;
		}

		// Assert
		result.ShouldBe("Postgres");
	}

	[Fact]
	public void ParseFromStringCorrectly()
	{
		// Arrange & Act & Assert
		Enum.Parse<SupportedDatabase>("Unknown").ShouldBe(SupportedDatabase.Unknown);
		Enum.Parse<SupportedDatabase>("Postgres").ShouldBe(SupportedDatabase.Postgres);
		Enum.Parse<SupportedDatabase>("SqlServer").ShouldBe(SupportedDatabase.SqlServer);
	}

	[Fact]
	public void ThrowOnParsingInvalidStrings()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<SupportedDatabase>("NonExisting"));
	}
}
