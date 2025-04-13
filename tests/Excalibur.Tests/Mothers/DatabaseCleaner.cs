using System.Data;

using Dapper;

namespace Excalibur.Tests.Mothers;

public static class DatabaseCleaner
{
	public static async Task CleanupUsersAsync(IDbConnection connection) =>
		_ = await connection.ExecuteAsync("DELETE FROM Users;").ConfigureAwait(false);

	public static async Task CleanupDataTasksAsync(IDbConnection connection) =>
		_ = await connection.ExecuteAsync("DELETE FROM DataProcessor.DataTaskRequests;").ConfigureAwait(false);

	public static async Task ClearOutboxTablesAsync(IDbConnection connection)
	{
		_ = await connection.ExecuteAsync("DELETE FROM Outbox").ConfigureAwait(true);
		_ = await connection.ExecuteAsync("DELETE FROM OutboxDeadLetter").ConfigureAwait(true);
	}
}
