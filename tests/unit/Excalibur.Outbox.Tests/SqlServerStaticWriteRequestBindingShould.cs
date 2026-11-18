// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Outbox.Tests;

/// <summary>Compares actual closed request binding against the unchanged Dapper command metadata.</summary>
/// <remarks>Reflection constructs managed test requests only. This is not a database or NativeAOT pass.</remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerStaticWriteRequestBindingShould
{
	public static TheoryData<string> Requests() =>
	[
		"CleanupTransportDeliveriesRequest",
		"CleanupSentMessagesRequest",
		"FencedMarkMessageDeadLetteredRequest",
		"FencedMarkMessageFailedRequest",
		"GetOutboxStatisticsRequest",
		"InsertOutboxMessageRequest",
		"InsertOutboxMessageRequest:Mapped",
		"InsertTransportDeliveryRequest",
		"MarkMessageDeadLetteredRequest",
		"MarkMessageFailedRequest",
		"MarkMessageSentRequest",
		"MarkTransportFailedRequest",
		"MarkTransportSentRequest",
		"MarkTransportSkippedRequest",
		"GetOutboxFenceHighWaterRequest",
		"ResetOutboxFenceHighWaterRequest",
		"UpdateAggregateStatusRequest",
		"SqlServerClaimCompletionRequest:Sent",
		"SqlServerClaimCompletionRequest:Failed",
		"SqlServerClaimCompletionRequest:FailedScheduled",
		"SqlServerClaimCompletionRequest:DeadLettered",
		"SqlServerTransactionalFenceRequest",
	];

	[Theory]
	[MemberData(nameof(Requests))]
	public async Task BindTheSamePhysicalValuesTypesDirectionsAndSizesAsItsDeclaredCommand(string name)
	{
		var mappedInsert = name == "InsertOutboxMessageRequest:Mapped";
		var requestName = name.Split(':')[0];
		var strongRequest = requestName is "SqlServerClaimCompletionRequest" or "SqlServerTransactionalFenceRequest";
		using var borrowedTransaction = new BorrowedTransaction();
		var type = typeof(SqlServerOutboxStore).Assembly.GetTypes().Single(candidate => candidate.Name == requestName);
		var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		var constructor = requestName == "InsertOutboxMessageRequest"
			? constructors.Single(candidate => candidate.GetParameters().Select(parameter => parameter.ParameterType)
				.SequenceEqual(new[] { typeof(string), mappedInsert ? typeof(OutboxMessage) : typeof(OutboundMessage),
					typeof(IDbTransaction), typeof(int), typeof(CancellationToken) }))
			: constructors.Single();
		var request = constructor.Invoke(constructor.GetParameters().Select(parameter => strongRequest ? StrongArgument(parameter, name, borrowedTransaction) : Argument(parameter)).ToArray());
		var definition = (CommandDefinition)type.GetProperty("Command")!.GetValue(request)!;
		using var original = new Command();
		using var originalConnection = new OpenConnection(original);
		_ = await originalConnection.ExecuteAsync(definition);

		using var candidate = new Command();
		using var candidateConnection = new OpenConnection(candidate);
		var resolve = (Delegate)type.GetProperty("ResolveAsync")!.GetValue(request)!;
		var resolvedTask = (Task)resolve.DynamicInvoke(candidateConnection)!;
		await resolvedTask;
		if (strongRequest)
		{
			var value = resolvedTask.GetType().GetProperty("Result")!.GetValue(resolvedTask);
			if (requestName == "SqlServerClaimCompletionRequest") { value.ShouldBe(OutboxCompletionOutcome.Applied); }
			else { value.ShouldBe(73L); }
			original.ObservedTransaction.ShouldBeSameAs(borrowedTransaction);
			candidate.ObservedTransaction.ShouldBeSameAs(borrowedTransaction);
			borrowedTransaction.DisposeCalls.ShouldBe(0);
			borrowedTransaction.CommitCalls.ShouldBe(0);
			borrowedTransaction.RollbackCalls.ShouldBe(0);
			originalConnection.CloseCalls.ShouldBe(0);
			original.WasDisposed.ShouldBeTrue();
			AssertStrongRequestValues(original, name);
			AssertStrongRequestValues(candidate, name);
		}

		candidate.Executed.ShouldBeTrue();
		candidate.CommandText.ShouldBe(original.CommandText);
		candidate.CommandTimeout.ShouldBe(original.CommandTimeout);
		candidate.CommandType.ShouldBe(original.CommandType);
		candidate.Observed.Keys.Order(StringComparer.Ordinal).ShouldBe(original.Observed.Keys.Order(StringComparer.Ordinal));
		foreach (var (key, expected) in original.Observed)
		{
			var actual = candidate.Observed[key];
			actual.Type.ShouldBe(expected.Type, name + ":" + key);
			actual.Direction.ShouldBe(expected.Direction, name + ":" + key);
			actual.Size.ShouldBe(expected.Size, name + ":" + key);
			if (expected.Value is byte[] bytes) { ((byte[])actual.Value!).ShouldBe(bytes); }
			else { actual.Value.ShouldBe(expected.Value, name + ":" + key); }
		}
		if (mappedInsert)
		{
			AssertMappedInsertValues(original);
			AssertMappedInsertValues(candidate);
		}
		candidate.WasDisposed.ShouldBeTrue();
		candidateConnection.CloseCalls.ShouldBe(0, "the request must keep its already-open borrowed connection");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task BindScheduledTimestampWithOneExplicitSqlTypeForNullAndNonNullValues(bool scheduled)
	{
		DateTimeOffset? timestamp = scheduled
			? new DateTimeOffset(2026, 11, 13, 1, 2, 3, TimeSpan.FromMinutes(330))
			: null;
		var message = new OutboundMessage("event", [1], "destination") { ScheduledAt = timestamp };
		var request = new Excalibur.Outbox.SqlServer.Requests.InsertOutboxMessageRequest(
			"dbo.TestTable", message, null, 30, CancellationToken.None);
		using var declared = new Command();
		using var declaredConnection = new OpenConnection(declared);
		_ = await declaredConnection.ExecuteAsync(request.Command);
		using var actual = new Command();
		using var actualConnection = new OpenConnection(actual);
		_ = await request.ResolveAsync(actualConnection);
		var expected = timestamp.HasValue ? (object)timestamp.Value : DBNull.Value;
		declared.Observed["ScheduledAt"].Type.ShouldBe(DbType.DateTimeOffset);
		actual.Observed["ScheduledAt"].Type.ShouldBe(DbType.DateTimeOffset);
		actual.Observed["ScheduledAt"].Value.ShouldBe(expected);
		declared.Observed["ScheduledAt"].Value.ShouldBe(expected);
		((SqlParameter)actual.Parameters["ScheduledAt"]).SqlDbType.ShouldBe(SqlDbType.DateTimeOffset);
	}

	private static object? Argument(ParameterInfo parameter)
	{
		var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
		if (type == typeof(string)) { return parameter.Name?.Contains("Table", StringComparison.OrdinalIgnoreCase) == true ? "dbo.TestTable" : "value:" + parameter.Name; }
		if (type == typeof(int)) { return 5; }
		if (type == typeof(long)) { return 7L; }
		if (type == typeof(double)) { return 3.25; }
		if (type == typeof(bool)) { return false; }
		if (type == typeof(DateTimeOffset)) { return new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.FromMinutes(330)); }
		if (type == typeof(byte[])) { return new byte[] { 0, 255, 128, 1 }; }
		if (type == typeof(CancellationToken)) { return CancellationToken.None; }
		if (type == typeof(IDbTransaction)) { return null; }
		if (type == typeof(OutboxMessage))
		{
			return new OutboxMessage
			{
				MessageId = "mapped-id", MessageType = "mapped-type", MessageBody = [0, 255, 128, 1],
				MessageMetadata = "{\"header\":\"value\"}", Destination = null,
				CreatedAt = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.FromMinutes(330)),
				ScheduledAt = new DateTimeOffset(2026, 11, 13, 2, 3, 4, TimeSpan.FromMinutes(-420)),
				TenantId = "mapped-tenant", CorrelationId = "mapped-correlation", CausationId = "mapped-causation",
				Priority = 9, Attempts = 37, TargetTransports = "[\"broker-a\",\"broker-b\"]", IsMultiTransport = true,
				PartitionKey = "mapped-partition", GroupKey = "mapped-group", SequenceNumber = 1234567890123L,
			};
		}
		if (type == typeof(OutboundMessage))
		{
			return new OutboundMessage { Id = "id", MessageType = "type", Payload = [0, 255, 128, 1], Destination = "destination", TenantId = "tenant" };
		}
		if (type == typeof(OutboundMessageTransport))
		{
			return new OutboundMessageTransport { Id = "delivery", MessageId = "id", TransportName = "transport", Destination = "destination", CreatedAt = DateTimeOffset.UtcNow };
		}
		throw new InvalidOperationException("Review a test factory for constructor argument " + parameter.Name + ":" + type.Name);
	}

	private static void AssertMappedInsertValues(Command command)
	{
		// Independent literal oracle: comparing two adapters alone would miss a field omitted by both.
		var expected = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Id"] = "mapped-id", ["MessageType"] = "mapped-type", ["Payload"] = new byte[] { 0, 255, 128, 1 },
			["Headers"] = "{\"header\":\"value\"}", ["Destination"] = DBNull.Value,
			["CreatedAt"] = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.FromMinutes(330)),
			["ScheduledAt"] = new DateTimeOffset(2026, 11, 13, 2, 3, 4, TimeSpan.FromMinutes(-420)),
			["Status"] = (int)OutboxStatus.Staged, ["RetryCount"] = 0,
			["TenantId"] = "mapped-tenant", ["CorrelationId"] = "mapped-correlation", ["CausationId"] = "mapped-causation",
			["Priority"] = 9, ["TargetTransports"] = "[\"broker-a\",\"broker-b\"]", ["IsMultiTransport"] = true,
			["PartitionKey"] = "mapped-partition", ["GroupKey"] = "mapped-group", ["SequenceNumber"] = 1234567890123L,
		};
		command.Observed.Count.ShouldBe(18);
		command.Observed.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Keys.Order(StringComparer.Ordinal));
		foreach (var (key, value) in expected)
		{
			var actual = command.Observed[key].Value;
			if (value is byte[] bytes) { actual.ShouldBeOfType<byte[]>().ShouldBe(bytes); }
			else if (value is DateTimeOffset timestamp)
			{
				var observed = actual.ShouldBeOfType<DateTimeOffset>();
				observed.ShouldBe(timestamp);
				observed.Offset.ShouldBe(timestamp.Offset);
				command.Observed[key].Type.ShouldBe(DbType.DateTimeOffset);
				((SqlParameter)command.Parameters[key]).SqlDbType.ShouldBe(SqlDbType.DateTimeOffset);
			}
			else { actual.ShouldBe(value, key); }
		}
	}

	private static object? StrongArgument(ParameterInfo parameter, string name, BorrowedTransaction transaction)
	{
		if (parameter.ParameterType == typeof(IDbTransaction)) { return transaction; }
		return parameter.Name switch
		{
			"table" or "fenceTable" => "dbo.TestTable",
			"messageId" => "strong-message",
			"claimIdentity" => "Claim-\U0001F600 ",
			"transition" => Enum.Parse(parameter.ParameterType, name.EndsWith(":FailedScheduled", StringComparison.Ordinal) ? "Failed" : name.Split(':')[1]),
			"error" => "CONTROLLED-ERROR",
			"attempts" => 7,
			"nextAttemptAt" => name.EndsWith(":FailedScheduled", StringComparison.Ordinal) ? DateTimeOffset.UtcNow.AddMinutes(1) : null,
			"floorSeconds" => 31,
			"leaseSeconds" => 127,
			"commandTimeout" or "timeout" => 13,
			"fencingToken" => name.EndsWith(":Sent", StringComparison.Ordinal) ? 73L : null,
			"scope" => "dbo.StrongScope",
			"token" when parameter.ParameterType == typeof(long) => 73L,
			"token" or "cancellationToken" => CancellationToken.None,
			_ => throw new InvalidOperationException("Review strong constructor argument: " + parameter.Name),
		};
	}

	private static void AssertStrongRequestValues(Command command, string name)
	{
		command.CommandTimeout.ShouldBe(13);
		if (name == "SqlServerTransactionalFenceRequest")
		{
			command.Observed.Keys.Order(StringComparer.Ordinal).ShouldBe(["HighWater", "Scope", "Token"]);
			command.Observed["Scope"].Value.ShouldBe("dbo.StrongScope");
			command.Observed["Token"].Value.ShouldBe(73L);
			command.Observed["HighWater"].Direction.ShouldBe(ParameterDirection.Output);
			command.Observed["HighWater"].Type.ShouldBe(DbType.Int64);
			return;
		}
		var keys = new List<string> { "MessageId", "ClaimIdentity", "LeaseSeconds", "LastAttemptAt", "Outcome" };
		if (name.EndsWith(":Sent", StringComparison.Ordinal)) { keys.Add("FencingToken"); }
		else
		{
			keys.AddRange(["ErrorMessage", "RetryCount"]);
			if (name.Contains(":Failed", StringComparison.Ordinal)) { keys.Add("FloorSeconds"); }
			if (name.EndsWith(":FailedScheduled", StringComparison.Ordinal)) { keys.Add("NextAttemptDelayMs"); }
		}
		command.Observed.Keys.Order(StringComparer.Ordinal).ShouldBe(keys.Order(StringComparer.Ordinal));
		command.Observed["MessageId"].Value.ShouldBe("strong-message");
		command.Observed["ClaimIdentity"].Value.ShouldBe("Claim-\U0001F600 ");
		command.Observed["LeaseSeconds"].Value.ShouldBe(127);
		command.Observed["LastAttemptAt"].Type.ShouldBe(DbType.DateTimeOffset);
		command.Observed["LastAttemptAt"].Value.ShouldBeOfType<DateTimeOffset>().Offset.ShouldBe(TimeSpan.Zero);
		command.Observed["Outcome"].Direction.ShouldBe(ParameterDirection.Output);
		command.Observed["Outcome"].Type.ShouldBe(DbType.Int32);
		if (keys.Contains("ErrorMessage")) { command.Observed["ErrorMessage"].Value.ShouldBe("CONTROLLED-ERROR"); }
		if (keys.Contains("RetryCount")) { command.Observed["RetryCount"].Value.ShouldBe(7); }
		if (keys.Contains("FloorSeconds")) { command.Observed["FloorSeconds"].Value.ShouldBe(31); }
		if (keys.Contains("FencingToken")) { command.Observed["FencingToken"].Value.ShouldBe(73L); }
		if (keys.Contains("NextAttemptDelayMs"))
		{
			command.Observed["NextAttemptDelayMs"].Value.ShouldBeOfType<int>().ShouldBeInRange(1, 60000);
		}
	}

	private sealed class BorrowedTransaction : DbTransaction
	{
		internal int DisposeCalls { get; private set; }
		internal int CommitCalls { get; private set; }
		internal int RollbackCalls { get; private set; }
		public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
		protected override DbConnection? DbConnection => null;
		public override void Commit() => CommitCalls++;
		public override void Rollback() => RollbackCalls++;
		protected override void Dispose(bool disposing) { DisposeCalls++; base.Dispose(disposing); }
	}

	private sealed record Shape(object? Value, DbType Type, ParameterDirection Direction, int Size);

	private sealed class Command : DbCommand
	{
		internal Dictionary<string, Shape> Observed { get; } = new(StringComparer.Ordinal);
		internal bool Executed { get; private set; }
		internal bool WasDisposed { get; private set; }
		internal DbTransaction? ObservedTransaction { get; private set; }
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; } = 30;
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; }
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection { get; } = new Parameters();
		protected override DbParameter CreateDbParameter() => new SqlParameter();
		public override void Cancel() { }
		public override void Prepare() { }
		public override int ExecuteNonQuery() => throw new NotSupportedException();
		public override object? ExecuteScalar() => throw new NotSupportedException();
		public override Task<int> ExecuteNonQueryAsync(CancellationToken token) { Observe(token); return Task.FromResult(3); }
		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken token)
		{
			Observe(token);
			var table = new DataTable();
			_ = table.Columns.Add("First", typeof(long));
			_ = table.Columns.Add("Second", typeof(int));
			_ = table.Columns.Add("Third", typeof(bool));
			_ = table.Columns.Add("Fourth", typeof(bool));
			_ = table.Columns.Add("Fifth", typeof(int));
			_ = table.Columns.Add("Sixth", typeof(double));
			_ = table.Columns.Add("Seventh", typeof(DateTimeOffset));
			_ = table.Rows.Add(7L, 1, true, false, 0, 2.5, DateTimeOffset.UtcNow);
			return Task.FromResult<DbDataReader>(table.CreateDataReader());
		}
		private void Observe(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Executed = true;
			ObservedTransaction = DbTransaction;
			foreach (DbParameter parameter in Parameters)
			{
				Observed[parameter.ParameterName] = new Shape(parameter.Value, parameter.DbType, parameter.Direction, parameter.Size);
				if (parameter.Direction == ParameterDirection.Output) { parameter.Value = parameter.DbType == DbType.Int32 ? (object)1 : parameter.ParameterName == "OutHigh" ? 7L : parameter.ParameterName == "HighWater" ? 73L : 1L; }
			}
		}
		protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
	}

	private sealed class OpenConnection(Command command) : IDbConnection
	{
		internal int CloseCalls { get; private set; }
		[AllowNull] public string ConnectionString { get; set; } = "test";
		public int ConnectionTimeout => 30;
		public string Database => "test";
		public ConnectionState State => ConnectionState.Open;
		public IDbTransaction BeginTransaction() => throw new NotSupportedException();
		public IDbTransaction BeginTransaction(IsolationLevel isolation) => throw new NotSupportedException();
		public void ChangeDatabase(string database) => throw new NotSupportedException();
		public void Close() => CloseCalls++;
		public IDbCommand CreateCommand() => command;
		public void Open() => throw new NotSupportedException();
		public void Dispose() { }
	}

	private sealed class Parameters : DbParameterCollection
	{
		private readonly List<DbParameter> _items = [];
		public override int Count => _items.Count;
		public override object SyncRoot => this;
		public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
		public override void AddRange(Array values) { foreach (var value in values) { _ = Add(value!); } }
		public override void Clear() => _items.Clear();
		public override bool Contains(object value) => _items.Contains((DbParameter)value);
		public override bool Contains(string value) => IndexOf(value) >= 0;
		public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
		public override IEnumerator GetEnumerator() => _items.GetEnumerator();
		public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
		public override int IndexOf(string name) => _items.FindIndex(parameter => parameter.ParameterName == name);
		public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
		public override void Remove(object value) => _items.Remove((DbParameter)value);
		public override void RemoveAt(int index) => _items.RemoveAt(index);
		public override void RemoveAt(string name) => _items.RemoveAt(IndexOf(name));
		protected override DbParameter GetParameter(int index) => _items[index];
		protected override DbParameter GetParameter(string name) => _items[IndexOf(name)];
		protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
		protected override void SetParameter(string name, DbParameter value) => _items[IndexOf(name)] = value;
	}
}
