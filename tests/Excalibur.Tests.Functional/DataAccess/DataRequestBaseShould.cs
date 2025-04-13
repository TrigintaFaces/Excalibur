using System.Data;

using Dapper;

using Excalibur.DataAccess;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Functional.DataAccess;

public class DataRequestBaseShould
{
	[Fact]
	public async Task ResolveDataCorrectly()
	{
		// Arrange
		var fakeConnection = A.Fake<IDbConnection>();
		var request = new TestRequest();

		// Act
		var result = await request.ResolveAsync(fakeConnection).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Resolved Data");
	}

	[Fact]
	public void ThrowExceptionWhenInvalidCommand()
	{
		// Arrange
		var request = new TestRequest();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => request.CreateCommand(null!, null));
	}

	[Fact]
	public void AllowParameterInitialization()
	{
		// Arrange
		var request = new TestRequest();
		var parameters = new DynamicParameters();
		parameters.Add("@Name", "John Doe");

		// Act
		request.Parameters = parameters;

		// Assert
		request.Parameters.ShouldBe(parameters);
	}

	private sealed class TestRequest : DataRequest<string>
	{
		public TestRequest()
		{
			ResolveAsync = async conn =>
			{
				await Task.Delay(10).ConfigureAwait(true);
				return "Resolved Data";
			};
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
