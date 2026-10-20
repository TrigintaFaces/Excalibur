// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Covers what <see cref="DataOrchestrationManager.AddDataTaskForRecordTypeAsync"/> writes, and where.
/// </summary>
/// <remarks>
/// The arms pin the two halves that a consumer cannot see from the outside: the identifier is only
/// returned when a row was actually affected (safety), and a normal enqueue reaches the configured
/// schema-qualified table rather than the connection's default schema (liveness). The second is the
/// one that keeps mattering — a task written to an unexpected table looks exactly like a task that
/// was never written.
/// </remarks>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataTaskEnqueueShould : UnitTestBase
{
	[Fact]
	public async Task WriteToTheConfiguredSchemaQualifiedTable()
	{
		var connection = new CountingDbConnection(affectedRows: 1);
		var manager = CreateManager(connection, new DataProcessingOptions());

		var dataTaskId = await manager.AddDataTaskForRecordTypeAsync("Orders", CancellationToken.None);

		dataTaskId.ShouldNotBe(Guid.Empty);
		connection.CommandTexts.Count.ShouldBe(1);
		connection.CommandTexts[0].ShouldContain("[DataProcessor].[DataTaskRequests]");
	}

	[Fact]
	public async Task HonourTheConfiguredSchemaAndTableName()
	{
		var connection = new CountingDbConnection(affectedRows: 1);
		var manager = CreateManager(
			connection,
			new DataProcessingOptions { SchemaName = "Queue", TableName = "Backfills" });

		_ = await manager.AddDataTaskForRecordTypeAsync("Orders", CancellationToken.None);

		connection.CommandTexts[0].ShouldContain("[Queue].[Backfills]");
	}

	[Fact]
	public async Task RefuseToReturnAnIdentifierWhenNoRowWasAffected()
	{
		var connection = new CountingDbConnection(affectedRows: 0);
		var manager = CreateManager(connection, new DataProcessingOptions());

		var failure = await Should.ThrowAsync<InvalidOperationException>(
			() => manager.AddDataTaskForRecordTypeAsync("Orders", CancellationToken.None));

		failure.Message.ShouldContain("[DataProcessor].[DataTaskRequests]");
		failure.Message.ShouldContain("Orders");
	}

	[Fact]
	public async Task ReturnTheIdentifierWhenTheCountIsSuppressed()
	{
		// SET NOCOUNT ON reports -1 rather than a row count. That is a suppressed count, not a
		// refusal, so the guard above must not read it as one.
		var connection = new CountingDbConnection(affectedRows: -1);
		var manager = CreateManager(connection, new DataProcessingOptions());

		var dataTaskId = await manager.AddDataTaskForRecordTypeAsync("Orders", CancellationToken.None);

		dataTaskId.ShouldNotBe(Guid.Empty);
	}

	private static DataOrchestrationManager CreateManager(
		DbConnection connection,
		DataProcessingOptions options)
	{
		// A faked provider answers every GetService with a proxy, including the optional resilience
		// policy factory, which then fails to cast. An empty real container answers null, which is
		// the shape the manager's own fallback is written for.
		var services = new ServiceCollection().BuildServiceProvider();

		return new DataOrchestrationManager(
			() => connection,
			A.Fake<IDataProcessorRegistry>(),
			services,
			Microsoft.Extensions.Options.Options.Create(options),
			A.Fake<ILogger<DataOrchestrationManager>>());
	}

	/// <summary>
	/// A connection that records the SQL it is handed and reports a fixed affected-row count.
	/// </summary>
	private sealed class CountingDbConnection(int affectedRows) : DbConnection
	{
		private ConnectionState _state = ConnectionState.Open;

		public List<string> CommandTexts { get; } = [];

		internal int AffectedRows => affectedRows;

		[AllowNull]
		public override string ConnectionString { get; set; } = "Server=localhost;Database=TestDb";

		public override string Database => "TestDb";

		public override string DataSource => "TestSource";

		public override string ServerVersion => "1.0";

		public override ConnectionState State => _state;

		public override void ChangeDatabase(string databaseName) => _ = databaseName;

		public override void Close() => _state = ConnectionState.Closed;

		public override void Open() => _state = ConnectionState.Open;

		internal void Record(string commandText) => CommandTexts.Add(commandText);

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
			throw new NotSupportedException("No transaction is needed to count an insert.");

		protected override DbCommand CreateDbCommand() => new CountingDbCommand(this);
	}

	private sealed class CountingDbCommand(CountingDbConnection owner) : DbCommand
	{
		private readonly SqlParameterCollection _parameters = new SqlCommand().Parameters;

		[AllowNull]
		public override string CommandText { get; set; } = string.Empty;

		public override int CommandTimeout { get; set; }

		public override CommandType CommandType { get; set; } = CommandType.Text;

		public override bool DesignTimeVisible { get; set; }

		public override UpdateRowSource UpdatedRowSource { get; set; }

		protected override DbConnection? DbConnection { get; set; } = owner;

		protected override DbParameterCollection DbParameterCollection => _parameters;

		protected override DbTransaction? DbTransaction { get; set; }

		public override void Cancel()
		{
		}

		public override int ExecuteNonQuery()
		{
			owner.Record(CommandText ?? string.Empty);
			return owner.AffectedRows;
		}

		public override object? ExecuteScalar() => throw new NotSupportedException();

		public override void Prepare()
		{
		}

		protected override DbParameter CreateDbParameter() => new SqlParameter();

		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
			throw new NotSupportedException();
	}
}
