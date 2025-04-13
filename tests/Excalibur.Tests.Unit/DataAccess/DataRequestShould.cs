using System.Data;

using Dapper;

using Excalibur.DataAccess;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DataRequestShould
{
	[Fact]
	public void InheritFromDataRequestBase()
	{
		// Arrange & Act
		var request = new TestDataRequest();

		// Assert
		_ = request.ShouldBeAssignableTo<DataRequestBase<IDbConnection, string>>();
		_ = request.ShouldBeAssignableTo<IDataRequest<IDbConnection, string>>();
	}

	[Fact]
	public async Task ResolveAsyncShouldInvokeBaseResolveAsync()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var expectedResult = "Test Result";
		var request = new TestDataRequest(connection => Task.FromResult(expectedResult));

		// Act
		var result = await request.ResolveAsync(connection).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public void CommandShouldBeInitializedCorrectly()
	{
		// Arrange
		var sql = "SELECT * FROM TestTable WHERE Id = @Id";
		var parameters = new DynamicParameters();
		parameters.Add("@Id", 1);

		// Act
		var request = new TestDataRequest(sql, parameters);

		// Assert
		request.Command.CommandText.ShouldBe(sql);
		request.Parameters.ShouldBeSameAs(parameters);
	}

	[Fact]
	public void ShouldUseDefaultParametersWhenParametersAreNull()
	{
		// Arrange
		var sql = "SELECT * FROM TestTable";

		// Act
		var request = new TestDataRequest(sql, null);

		// Assert
		_ = request.Parameters.ShouldNotBeNull();
		request.Parameters.ParameterNames.ShouldBeEmpty();
	}

	[Fact]
	public void ShouldThrowArgumentExceptionWhenCommandTextIsNullOrWhitespace()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TestDataRequest(null, null));
		_ = Should.Throw<ArgumentException>(() => new TestDataRequest("", null));
		_ = Should.Throw<ArgumentException>(() => new TestDataRequest("   ", null));
	}

	private sealed class TestDataRequest : DataRequest<string>
	{
		public TestDataRequest()
		{
			Command = CreateCommand("SELECT 'Default'", null);
			ResolveAsync = connection => Task.FromResult("Default");
		}

		public TestDataRequest(string commandText, DynamicParameters parameters)
		{
			Command = CreateCommand(commandText, parameters);
			ResolveAsync = connection => Task.FromResult("Default");
		}

		public TestDataRequest(Func<IDbConnection, Task<string>> resolver)
		{
			Command = CreateCommand("SELECT 'Default'", null);
			ResolveAsync = resolver;
		}
	}
}
