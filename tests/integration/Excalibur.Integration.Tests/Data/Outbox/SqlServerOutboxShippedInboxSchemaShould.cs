// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;
using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
public sealed class SqlServerOutboxShippedInboxSchemaShould(SqlServerOutboxStoreContainerFixture fixture)
	: IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Executes the checked-in default inbox schema from a fixed repository path; no user input or dynamic SQL identifiers enter these script batches.")]
	public async Task CompleteAgainstTheActualDefaultSingleTenantInboxSchema()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);
		await using (var schema = new SqlConnection(fixture.ConnectionString))
		{
			await schema.OpenAsync().ConfigureAwait(false);
			foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(
				"src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.sql"))
			{
				await using var command = new SqlCommand(batch, schema);
				_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
		}

		var options = new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString };
		var inbox = new SqlServerInboxOptions { ConnectionString = fixture.ConnectionString };
		var store = new SqlServerOutboxStore(() => new SqlConnection(fixture.ConnectionString), options,
			payloadSerializer: null, inbox, NullLogger<SqlServerOutboxStore>.Instance);
		var id = "shipped-default-inbox-" + Guid.NewGuid().ToString("N");
		await store.StageMessageAsync(new OutboundMessage("message-type", [1, 2, 255], "destination") { Id = id },
			CancellationToken.None).ConfigureAwait(false);

		(await store.TryMarkSentAndReceivedAsync(id, new InboxEntry(id, "handler", "message-type", [1, 2, 255]),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue(
			"the public effectively-once completion must work with the schema shipped for the default inbox mode");
		await using var read = new SqlConnection(fixture.ConnectionString);
		await read.OpenAsync().ConfigureAwait(false);
		await using var verify = new SqlCommand(
			"SELECT COUNT(*) FROM [dbo].[inbox_messages] WHERE MessageId=@Id AND HandlerType='handler'", read);
		_ = verify.Parameters.AddWithValue("@Id", id);
		Convert.ToInt32(await verify.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBe(1);
	}
}
