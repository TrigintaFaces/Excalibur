using System.Data;

using Dapper;

using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DataRequestBaseShould
{
	[Fact]
	public void CreateCommandAndInitializeCommandDefinition()
	{
		// Arrange
		var request = new TestRequest();

		// Act
		var command = request.CreateCommand("SELECT * FROM Users");

		// Assert
		command.CommandText.ShouldBe("SELECT * FROM Users");
		command.CommandTimeout.ShouldBeNull();
		command.CommandType.ShouldBe(CommandType.Text);
		_ = command.Parameters.ShouldBeOfType<DynamicParameters>();
		((DynamicParameters)command.Parameters).ParameterNames.ShouldBeEmpty();
	}

	[Fact]
	public void CreateCommandAndUseProvidedParameters()
	{
		// Arrange
		var request = new TestRequest();
		var parameters = new DynamicParameters();
		parameters.Add("@UserId", 42);

		// Act
		var command = request.CreateCommand("SELECT * FROM Users WHERE Id = @UserId", parameters);

		// Assert
		request.Parameters.ShouldBe(parameters);
		command.CommandText.ShouldBe("SELECT * FROM Users WHERE Id = @UserId");
	}

	[Fact]
	public void CreateCommandWithTimeoutSuccessfully()
	{
		// Arrange
		var dataRequest = new TestRequest();

		// Act
		var command = dataRequest.CreateCommand("SELECT * FROM Test", commandTimeout: 30);

		// Assert
		command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public async Task ResolveAsyncAndReturnExpectedValue()
	{
		// Arrange
		var request = new TestRequest();

		// Act
		var result = await request.ResolveAsync(default!).ConfigureAwait(true);

		// Assert
		result.ShouldBe("TestData");
	}

	private sealed class TestRequest : DataRequestBase<IDbConnection, string>
	{
		public TestRequest()
		{
			ResolveAsync = conn => Task.FromResult("TestData");
		}

		public new CommandDefinition CreateCommand(
			string commandText,
			DynamicParameters? parameters = null,
			IDbTransaction? transaction = null,
			int? commandTimeout = null,
			CommandType? commandType = null,
			CommandFlags flags = CommandFlags.Buffered,
			CancellationToken cancellationToken = default) =>
			base.CreateCommand(commandText, parameters, transaction, commandTimeout, commandType, flags, cancellationToken);
	}
}
