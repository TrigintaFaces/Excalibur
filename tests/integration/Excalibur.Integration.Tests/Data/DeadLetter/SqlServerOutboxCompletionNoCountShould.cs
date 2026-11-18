// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using Dapper;
using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Tests.Shared.Fixtures;
using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerOutboxCompletionNoCountShould(SqlServerContainerFixture fixture) : IAsyncLifetime
{
	private readonly string _suffix = Guid.NewGuid().ToString("N");
	private string OutboxTable => "OutboxCompletion_" + _suffix;
	private string InboxTable => "InboxCompletion_" + _suffix;
	private const string MessageId = "message";
	private const string Handler = "handler";
	private static readonly DateTimeOffset Lease = new(2026, 11, 13, 9, 10, 11, TimeSpan.Zero);

	[Theory]
	[InlineData(false, -1)]
	[InlineData(true, -1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	[InlineData(false, 5)]
	[InlineData(true, 5)]
	public async Task RefuseMissingOrTerminalOutboxWithoutInsertingInbox(bool noCount, int status)
	{
		if (status >= 0) { await SeedOutboxAsync(status).ConfigureAwait(false); }
		var store = Store(noCount);
		var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.TryMarkSentAndReceivedAsync(MessageId, Entry(), CancellationToken.None).ConfigureAwait(false));
		error.Message.ShouldBe($"Message {MessageId} not found, already sent, or dead-lettered.",
			"rollback cleanup must preserve the actual refusal rather than replace it with a second rollback failure");
		(await InboxCountAsync().ConfigureAwait(false)).ShouldBe(0);
		if (status >= 0) { await AssertOriginalOutboxAsync(status).ConfigureAwait(false); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CommitInboxAndReleaseLeaseOnlyAfterOneEligibleOutboxMutation(bool noCount)
	{
		await SeedOutboxAsync(0).ConfigureAwait(false);
		(await Store(noCount).TryMarkSentAndReceivedAsync(MessageId, Entry(), CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
		(await InboxCountAsync().ConfigureAwait(false)).ShouldBe(1);
		await using var connection = new SqlConnection(fixture.ConnectionString);
		(await connection.ExecuteScalarAsync<int>($"SELECT Status FROM [dbo].[{OutboxTable}] WHERE Id=@Id", new { Id = MessageId }).ConfigureAwait(false)).ShouldBe(2);
		(await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [dbo].[{OutboxTable}] WHERE Id=@Id AND SentAt IS NOT NULL AND LeasedAt IS NULL AND LeasedBy IS NULL AND LastError IS NULL", new { Id = MessageId }).ConfigureAwait(false)).ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RestoreOutboxStateAndLeaseIfInboxInsertConflicts(bool noCount)
	{
		await SeedOutboxAsync(0).ConfigureAwait(false);
		await using (var setup = new SqlConnection(fixture.ConnectionString))
		{
			_ = await setup.ExecuteAsync($"INSERT INTO [dbo].[{InboxTable}] (MessageId, HandlerType, MessageType, Payload, ReceivedAt) VALUES (@MessageId,@HandlerType,N'message-type',0x0102FF,SYSUTCDATETIME())", new { MessageId, HandlerType = Handler }).ConfigureAwait(false);
		}
		var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store(noCount).TryMarkSentAndReceivedAsync(MessageId, Entry(), CancellationToken.None).ConfigureAwait(false));
		error.Message.ShouldBe($"Inbox entry already exists for message '{MessageId}' and handler '{Handler}'.");
		error.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBeOneOf(2601, 2627);
		(await InboxCountAsync().ConfigureAwait(false)).ShouldBe(1);
		await AssertOriginalOutboxAsync(0).ConfigureAwait(false);
	}

	[Theory]
	[InlineData(false, -1)]
	[InlineData(true, -1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	[InlineData(false, 5)]
	[InlineData(true, 5)]
	public async Task PreserveTheRefusalWithTheLargestValidSqlTimeout(bool noCount, int status)
	{
		if (status >= 0) { await SeedOutboxAsync(status).ConfigureAwait(false); }
		var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store(noCount, int.MaxValue).TryMarkSentAndReceivedAsync(MessageId, Entry(), CancellationToken.None).ConfigureAwait(false));
		error.Message.ShouldBe($"Message {MessageId} not found, already sent, or dead-lettered.");
		(await InboxCountAsync().ConfigureAwait(false)).ShouldBe(0);
		if (status >= 0) { await AssertOriginalOutboxAsync(status).ConfigureAwait(false); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveTheOriginalSqlFailureWhenTheServerAlreadyRolledBack(bool noCount)
	{
		await SeedOutboxAsync(0).ConfigureAwait(false);
		await using var setup = new SqlConnection(fixture.ConnectionString);
		_ = await setup.ExecuteAsync($"""
			CREATE TRIGGER [dbo].[Rollback_{_suffix}] ON [dbo].[{InboxTable}] AFTER INSERT AS
			BEGIN
			    ROLLBACK TRANSACTION;
			    THROW 51001, 'original inbox failure after server rollback', 1;
			END;
			""").ConfigureAwait(false);
		var error = await Should.ThrowAsync<SqlException>(async () =>
			await Store(noCount, int.MaxValue).TryMarkSentAndReceivedAsync(MessageId, Entry(), CancellationToken.None).ConfigureAwait(false));
		error.Errors.Cast<SqlError>().Any(item => item.Number == 51001).ShouldBeTrue(
			"a rollback attempt against the already-completed transaction must not replace the original server failure");
		(await InboxCountAsync().ConfigureAwait(false)).ShouldBe(0);
		await AssertOriginalOutboxAsync(0).ConfigureAwait(false);
	}

	public async ValueTask InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("this control requires real SQL Server; infrastructure absence is a failure");
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE [dbo].[{OutboxTable}] (
			 Id NVARCHAR(255) NOT NULL PRIMARY KEY, Status INT NOT NULL, SentAt DATETIMEOFFSET NULL,
			 LastError NVARCHAR(MAX) NULL, LeasedAt DATETIMEOFFSET NULL, LeasedBy NVARCHAR(255) NULL);
			CREATE TABLE [dbo].[{InboxTable}] (
			 MessageId NVARCHAR(255) NOT NULL, HandlerType NVARCHAR(500) NOT NULL,
			 MessageType NVARCHAR(500) NOT NULL, Payload VARBINARY(MAX) NOT NULL, Metadata NVARCHAR(MAX) NULL,
			 ReceivedAt DATETIMEOFFSET NOT NULL, ProcessedAt DATETIMEOFFSET NULL, Status INT NOT NULL DEFAULT 0, RetryCount INT NOT NULL DEFAULT 0,
			 LastError NVARCHAR(MAX) NULL, LastAttemptAt DATETIMEOFFSET NULL, NextAttemptAt DATETIMEOFFSET NULL, LeaseExpiresAtUtc DATETIMEOFFSET NULL,
			 CorrelationId NVARCHAR(255) NULL, Source NVARCHAR(255) NULL,
			 PRIMARY KEY (MessageId,HandlerType));
			""").ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS [dbo].[{InboxTable}]; DROP TABLE IF EXISTS [dbo].[{OutboxTable}]").ConfigureAwait(false);
	}

	private static InboxEntry Entry() => new(MessageId, Handler, "message-type", new byte[] { 1, 2, 255 });

	private SqlServerOutboxStore Store(bool noCount, int commandTimeoutSeconds = 30) => new(
		() => ConfiguredConnection(noCount),
		new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString, Tables = { OutboxTableName = OutboxTable }, Processing = { CommandTimeoutSeconds = commandTimeoutSeconds } },
		payloadSerializer: null,
		new SqlServerInboxOptions { ConnectionString = fixture.ConnectionString, TableName = InboxTable },
		NullLogger<SqlServerOutboxStore>.Instance);

	[SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
		Justification = "SqlConnection.StateChange is a synchronous event contract; configure and verify this test-only session before the store's OpenAsync returns, without async-void work or a global database option.")]
	private SqlConnection ConfiguredConnection(bool noCount)
	{
		var connection = new SqlConnection(fixture.ConnectionString);
		connection.StateChange += (_, change) =>
		{
			if (change.CurrentState != ConnectionState.Open) { return; }
			using var setup = connection.CreateCommand();
			setup.CommandText = "SET NOCOUNT OFF; SELECT @@OPTIONS & 512";
			if (noCount) { setup.CommandText = "SET NOCOUNT ON; SELECT @@OPTIONS & 512"; }
			Convert.ToInt32(setup.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(noCount ? 512 : 0);
		};
		return connection;
	}

	private async Task SeedOutboxAsync(int status)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"INSERT INTO [dbo].[{OutboxTable}] (Id,Status,LastError,LeasedAt,LeasedBy) VALUES (@Id,@Status,'original-error',@Lease,'owner:claim')", new { Id = MessageId, Status = status, Lease }).ConfigureAwait(false);
	}

	private async Task<int> InboxCountAsync()
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [dbo].[{InboxTable}]").ConfigureAwait(false);
	}

	private async Task AssertOriginalOutboxAsync(int status)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		(await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [dbo].[{OutboxTable}] WHERE Id=@Id AND Status=@Status AND SentAt IS NULL AND LastError='original-error' AND LeasedAt=@Lease AND LeasedBy='owner:claim'", new { Id = MessageId, Status = status, Lease }).ConfigureAwait(false)).ShouldBe(1);
	}
}
