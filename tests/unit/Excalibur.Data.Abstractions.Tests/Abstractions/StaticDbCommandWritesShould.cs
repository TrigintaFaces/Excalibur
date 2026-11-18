// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Dapper;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Pattern", "Regression")]
public sealed class StaticDbCommandWritesShould
{
	[Theory]
	[InlineData(0, false)]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	public async Task MatchDapperSingleCardinalityWithoutMaterializingAnExtraRow(int count, bool allowDefault)
	{
		using var originalTable = Table(count);
		using var original = new Connection { Reader = originalTable.CreateDataReader() };
		int? expected = null;
		Exception? originalError = null;
		try
		{
			expected = allowDefault
				? await original.QuerySingleOrDefaultAsync<int>(new CommandDefinition("SELECT Value"))
				: await original.QuerySingleAsync<int>(new CommandDefinition("SELECT Value"));
		}
		catch (InvalidOperationException error) { originalError = error; }

		using var candidateTable = Table(count);
		using var candidate = new Connection { Reader = candidateTable.CreateDataReader() };
		var mapped = 0;
		int Materialize(DbDataReader reader) { mapped++; return reader.GetInt32(0); }
		int? actual = null;
		Exception? candidateError = null;
		try
		{
			actual = allowDefault
				? await StaticDbCommand.SingleOrDefaultAsync(candidate, new CommandDefinition("SELECT Value"), static _ => { }, Materialize)
				: await StaticDbCommand.SingleAsync(candidate, new CommandDefinition("SELECT Value"), static _ => { }, Materialize);
		}
		catch (InvalidOperationException error) { candidateError = error; }

		(candidateError?.GetType()).ShouldBe(originalError?.GetType());
		actual.ShouldBe(expected);
		mapped.ShouldBe(count == 0 ? 0 : 1);
		candidate.Command.WasDisposed.ShouldBeTrue();
		candidate.Reader.IsClosed.ShouldBeTrue();
		candidate.State.ShouldBe(ConnectionState.Closed);
	}

	[Fact]
	public async Task ReadOutputBeforeDisposalAndKeepItsObservableValueAfterward()
	{
		using var connection = new Connection
		{
			Outputs = command => command.Parameters["Affected"].Value = 1L,
		};
		var parameters = new DynamicParameters();
		parameters.Add("Affected", dbType: DbType.Int64, direction: ParameterDirection.Output);
		var result = await StaticDbCommand.ExecuteOutputsAsync(connection,
			new CommandDefinition("BEGIN :Affected := 1; END;", parameters),
			static command => StaticDbCommand.AddOutputParameter(command, "Affected", DbType.Int64),
			command =>
			{
				connection.Command.WasDisposed.ShouldBeFalse();
				return StaticDbCommand.Output<long>(command, parameters, "Affected", DbType.Int64);
			});

		result.ShouldBe(1);
		parameters.Get<long>("Affected").ShouldBe(1);
		connection.Command.NonqueryReturn.ShouldBe(-1, "an anonymous block's transport rowcount must not replace its declared OUT count");
		connection.Command.WasDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectMissingOutputInsteadOfInventingSuccessfulFenceEvidence()
	{
		using var connection = new Connection();
		var parameters = new DynamicParameters();
		var error = await Should.ThrowAsync<InvalidDataException>(() => StaticDbCommand.ExecuteOutputsAsync(connection,
			new CommandDefinition("BEGIN NULL; END;"),
			static command => StaticDbCommand.AddOutputParameter(command, "HighWater", DbType.Int64),
			command => StaticDbCommand.Output<long>(command, parameters, "HighWater", DbType.Int64)));

		error.Message.ShouldContain("HighWater");
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.State.ShouldBe(ConnectionState.Closed);
	}

	[Fact]
	public async Task BindAnExplicitNullableStringWithoutRelaxingRequiredStrings()
	{
		using var connection = new Connection();
		_ = await StaticDbCommand.ExecuteAsync(connection, new CommandDefinition("UPDATE t SET value=@Optional"),
			static command => StaticDbCommand.AddNullableStringParameter(command, "Optional", null));
		connection.Command.Observed["Optional"].ShouldBe(DBNull.Value);
		connection.Command.ObservedTypes["Optional"].ShouldBe(DbType.String);

		using var required = new Connection();
		_ = await Should.ThrowAsync<ArgumentException>(() => StaticDbCommand.ExecuteAsync(required,
			new CommandDefinition("UPDATE t SET value=@Required"),
			static command => StaticDbCommand.AddParameter(command, "Required", null, DbType.String)));
		required.Command.WasDisposed.ShouldBeTrue();
	}

	private static DataTable Table(int count)
	{
		var table = new DataTable();
		_ = table.Columns.Add("Value", typeof(int));
		for (var index = 0; index < count; index++) { _ = table.Rows.Add(index + 17); }
		return table;
	}

	private sealed class Connection : DbConnection
	{
		private ConnectionState _state = ConnectionState.Closed;
		internal Command Command { get; private set; } = null!;
		internal DbDataReader Reader { get; init; } = null!;
		internal Action<DbCommand>? Outputs { get; init; }
		[AllowNull] public override string ConnectionString { get; set; } = "";
		public override string Database => "test";
		public override string DataSource => "test";
		public override string ServerVersion => "1";
		public override ConnectionState State => _state;
		public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
		public override void Open() => _state = ConnectionState.Open;
		public override Task OpenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
		public override void Close() => _state = ConnectionState.Closed;
		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
		protected override DbCommand CreateDbCommand() => Command = new Command(this);
	}

	private sealed class Command(Connection owner) : DbCommand
	{
		internal bool WasDisposed { get; private set; }
		internal int NonqueryReturn => -1;
		internal Dictionary<string, object> Observed { get; } = new(StringComparer.Ordinal);
		internal Dictionary<string, DbType> ObservedTypes { get; } = new(StringComparer.Ordinal);
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; } = 30;
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; } = owner;
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection { get; } = new Parameters();
		protected override DbParameter CreateDbParameter() => new Parameter();
		public override void Cancel() { }
		public override void Prepare() { }
		public override object? ExecuteScalar() => throw new NotSupportedException();
		public override int ExecuteNonQuery() => throw new NotSupportedException();
		public override Task<int> ExecuteNonQueryAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			foreach (DbParameter parameter in Parameters)
			{
				Observed[parameter.ParameterName] = parameter.Value!;
				ObservedTypes[parameter.ParameterName] = parameter.DbType;
			}
			owner.Outputs?.Invoke(this);
			return Task.FromResult(NonqueryReturn);
		}
		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			return Task.FromResult(owner.Reader);
		}
		protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
	}

	private sealed class Parameter : DbParameter
	{
		public override DbType DbType { get; set; }
		public override ParameterDirection Direction { get; set; }
		public override bool IsNullable { get; set; }
		[AllowNull] public override string ParameterName { get; set; } = "";
		[AllowNull] public override string SourceColumn { get; set; } = "";
		public override object? Value { get; set; }
		public override bool SourceColumnNullMapping { get; set; }
		public override int Size { get; set; }
		public override void ResetDbType() => DbType = DbType.Object;
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
		public override int IndexOf(string parameterName) => _items.FindIndex(parameter => parameter.ParameterName == parameterName);
		public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
		public override void Remove(object value) => _items.Remove((DbParameter)value);
		public override void RemoveAt(int index) => _items.RemoveAt(index);
		public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
		protected override DbParameter GetParameter(int index) => _items[index];
		protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
		protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
		protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
	}
}
