// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Shouldly;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Outbox.Oracle;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>Compares actual closed request binding against the unchanged Dapper command metadata.</summary>
/// <remarks>Reflection constructs managed test requests only. This is not a database or NativeAOT pass.</remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OracleStaticWriteRequestBindingShould
{
	public static TheoryData<string> Requests() =>
	[
		"DeleteOutboxMessage",
		"FencedDeleteOutboxMessage",
		"FencedMarkMessageDeadLettered",
		"FencedSetOutboxMessageFailed",
		"GetOutboxStatistics",
		"IncrementOutboxMessageAttempts",
		"InsertOutboxMessage",
		"MarkMessageDeadLetteredRequest",
		"MoveOutboxMessageToDeadLetter",
		"GetOutboxFenceHighWaterRequest",
		"ResetOutboxFenceHighWaterRequest",
		"ScheduleOutboxMessage",
		"SetOutboxMessageBackoff",
		"SetOutboxMessageFailed",
	];

	[Theory]
	[MemberData(nameof(Requests))]
	public async Task BindTheSamePhysicalValuesTypesDirectionsAndSizesAsItsDeclaredCommand(string name)
	{
		var type = typeof(OracleOutboxStore).Assembly.GetTypes().Single(candidate => candidate.Name == name);
		var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
		var request = constructor.Invoke(constructor.GetParameters().Select(Argument).ToArray());
		var definition = (CommandDefinition)type.GetProperty("Command")!.GetValue(request)!;
		using var original = new Command();
		using var originalConnection = new OpenConnection(original);
		_ = await originalConnection.ExecuteAsync(definition);

		using var candidate = new Command();
		using var candidateConnection = new OpenConnection(candidate);
		var resolve = (Delegate)type.GetProperty("ResolveAsync")!.GetValue(request)!;
		await (Task)resolve.DynamicInvoke(candidateConnection)!;

		candidate.Executed.ShouldBeTrue();
		candidate.CommandText.ShouldBe(original.CommandText);
		candidate.CommandTimeout.ShouldBe(original.CommandTimeout);
		candidate.CommandType.ShouldBe(original.CommandType);
		candidate.Observed.Keys.Order(StringComparer.Ordinal).ShouldBe(original.Observed.Keys.Order(StringComparer.Ordinal));
		foreach (var (key, expected) in original.Observed)
		{
			var actual = candidate.Observed[key];
			actual.Type.ShouldBe(expected.Type, name + ":" + key);
			actual.OracleType.ShouldBe(expected.OracleType, name + ":" + key);
			actual.Direction.ShouldBe(expected.Direction, name + ":" + key);
			actual.Size.ShouldBe(expected.Size, name + ":" + key);
			if (expected.Value is byte[] bytes) { ((byte[])actual.Value!).ShouldBe(bytes); }
			else { actual.Value.ShouldBe(expected.Value, name + ":" + key); }
		}
		if (name is "InsertOutboxMessage" or "ScheduleOutboxMessage")
		{
			original.Observed["MessageBody"].OracleType.ShouldBe(OracleDbType.Blob);
			candidate.Observed["MessageBody"].OracleType.ShouldBe(OracleDbType.Blob);
		}
		candidate.WasDisposed.ShouldBeTrue();
		candidateConnection.CloseCalls.ShouldBe(0, "the request must keep its already-open borrowed connection");
	}

	[Theory]
	[InlineData("InsertOutboxMessage", 0, false)]
	[InlineData("InsertOutboxMessage", 1, false)]
	[InlineData("InsertOutboxMessage", 2, false)]
	[InlineData("InsertOutboxMessage", 0, true)]
	[InlineData("InsertOutboxMessage", 1, true)]
	[InlineData("InsertOutboxMessage", 2, true)]
	[InlineData("ScheduleOutboxMessage", 0, false)]
	[InlineData("ScheduleOutboxMessage", 1, false)]
	[InlineData("ScheduleOutboxMessage", 2, false)]
	[InlineData("ScheduleOutboxMessage", 0, true)]
	[InlineData("ScheduleOutboxMessage", 1, true)]
	[InlineData("ScheduleOutboxMessage", 2, true)]
	public async Task RefuseInvalidKnownBinaryParameterBeforeEitherExecutionPath(string name, int invalidKind, bool useDapper)
	{
		var type = typeof(OracleOutboxStore).Assembly.GetTypes().Single(candidate => candidate.Name == name);
		var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
		var request = constructor.Invoke(constructor.GetParameters().Select(Argument).ToArray());
		var definition = (CommandDefinition)type.GetProperty("Command")!.GetValue(request)!;
		var parameters = definition.Parameters.ShouldBeAssignableTo<DynamicParameters>().ShouldNotBeNull();
		object? invalid = invalidKind switch { 0 => null, 1 => DBNull.Value, _ => "wrong-clr-body" };
		parameters.Add("MessageBody", invalid);
		using var command = new Command();
		using var connection = new OpenConnection(command);
		Exception? observed = null;
		try
		{
			if (useDapper) { _ = await connection.ExecuteAsync(definition); }
			else
			{
				var resolve = (Delegate)type.GetProperty("ResolveAsync")!.GetValue(request)!;
				await (Task)resolve.DynamicInvoke(connection)!;
			}
		}
		catch (TargetInvocationException exception) { observed = exception.InnerException; }
		catch (Exception exception) { observed = exception; }
		observed.ShouldBeOfType<ArgumentException>().ShouldNotBeNull().ParamName.ShouldBe("MessageBody");
		command.Executed.ShouldBeFalse("invalid known CLR payloads must be refused before accepting execution");
		connection.CloseCalls.ShouldBe(0);
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

	private sealed record Shape(object? Value, DbType Type, ParameterDirection Direction, int Size, OracleDbType? OracleType);

	private sealed class Command : DbCommand
	{
		internal Dictionary<string, Shape> Observed { get; } = new(StringComparer.Ordinal);
		internal bool Executed { get; private set; }
		internal bool WasDisposed { get; private set; }
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; } = 30;
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; }
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection { get; } = new Parameters();
		protected override DbParameter CreateDbParameter() => new OracleParameter();
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
			foreach (DbParameter parameter in Parameters)
			{
				Observed[parameter.ParameterName] = new Shape(parameter.Value, parameter.DbType, parameter.Direction, parameter.Size, (parameter as OracleParameter)?.OracleDbType);
				if (parameter.Direction == ParameterDirection.Output) { parameter.Value = parameter.DbType == DbType.Int32 ? (object)1 : parameter.ParameterName == "OutHigh" ? 7L : 1L; }
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
