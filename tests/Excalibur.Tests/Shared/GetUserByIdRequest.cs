using System.Data;

using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.Tests.Shared;

public class GetUserByIdRequest : DataRequest<User>
{
	public GetUserByIdRequest(int userId)
	{
		Command = CreateCommand(
			"SELECT Id, Name FROM Users WHERE Id = @UserId",
			new DynamicParameters(new { UserId = userId }),
			commandType: CommandType.Text);

		ResolveAsync = async (connection) =>
			await connection.QueryFirstOrDefaultAsync<User?>(Command).ConfigureAwait(true);
	}
}
