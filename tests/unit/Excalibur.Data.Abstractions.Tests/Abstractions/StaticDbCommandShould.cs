// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using FakeItEasy;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Pattern", "Regression")]
[Collection("Static database command settings")]
public sealed class StaticDbCommandShould
{
	private const string CleanupKey = "Excalibur.Data.StaticDbCommand.CleanupFailures";
	private const string AlternateCleanupKey = "Excalibur.Data.StaticDbCommand.CleanupFailures.Secondary";

	[Fact]
	public void RejectAMissingRequiredKnownInputWithItsName()
	{
		var error = Should.Throw<ArgumentException>(() => StaticDbCommand.Input<int>(new DynamicParameters(), "BatchSize"));
		error.ParamName.ShouldBe("BatchSize");
		error.InnerException.ShouldBeOfType<KeyNotFoundException>();
	}

	[Theory]
	[InlineData(null, null, 30)]
	[InlineData(null, 71, 71)]
	[InlineData(47, 71, 47)]
	public async Task HonorExplicitThenGlobalThenProviderDefaultTimeout(int? requested, int? global, int expected)
	{
		var previous = SqlMapper.Settings.CommandTimeout;
		try
		{
			SqlMapper.Settings.CommandTimeout = global;
			using var connection = new RecordingConnection(true);
			_ = await StaticDbCommand.ExecuteAsync(connection, new CommandDefinition("UPDATE t SET n=1", commandTimeout: requested), static _ => { });
			connection.Command.CommandTimeout.ShouldBe(expected);
		}
		finally
		{
			SqlMapper.Settings.CommandTimeout = previous;
		}
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task BufferAndDisposeWhilePreservingBorrowedConnectionOwnership(bool initiallyOpen)
	{
		using var table = new DataTable();
		_ = table.Columns.Add("Value", typeof(int));
		_ = table.Rows.Add(17);
		using var reader = table.CreateDataReader();
		using var connection = new RecordingConnection(initiallyOpen) { Reader = reader };
		var values = await StaticDbCommand.QueryAsync(connection, new CommandDefinition("SELECT Value", commandTimeout: 47),
			static _ => { }, static row => row.GetInt32(0));

		values.ShouldBe([17]);
		reader.IsClosed.ShouldBeTrue();
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.Command.CommandTimeout.ShouldBe(47);
		connection.Command.Behavior.ShouldBe(CommandBehavior.Default);
		connection.OpenCalls.ShouldBe(initiallyOpen ? 0 : 1);
		connection.CloseCalls.ShouldBe(initiallyOpen ? 0 : 1);
		connection.State.ShouldBe(initiallyOpen ? ConnectionState.Open : ConnectionState.Closed);
	}

	[Fact]
	public async Task SupportAnAlreadyOpenInterfaceWrapperAndCarryItsTransactionAndInferredCommandType()
	{
		using var connection = new RecordingConnection(true);
		using var wrapper = new OpenConnectionWrapper(connection);
		using var transaction = connection.BeginTransaction();
		var definition = new CommandDefinition("StoredProcedureName", transaction: transaction, commandTimeout: 53);

		(await StaticDbCommand.ExecuteAsync(wrapper, definition, static _ => { })).ShouldBe(3);

		connection.Command.Transaction.ShouldBeSameAs(transaction);
		connection.Command.CommandType.ShouldBe(CommandType.StoredProcedure);
		connection.Command.CommandTimeout.ShouldBe(53);
		connection.CloseCalls.ShouldBe(0);
		connection.OpenCalls.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DisposeAndRestoreOwnershipAfterDriverFailure(bool initiallyOpen)
	{
		using var connection = new RecordingConnection(initiallyOpen) { FailExecution = true };

		_ = await Should.ThrowAsync<IOException>(() => StaticDbCommand.ExecuteAsync(connection,
			new CommandDefinition("UPDATE t SET n=1"), static _ => { }));

		connection.Command.WasDisposed.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(initiallyOpen ? 0 : 1);
	}

	[Fact]
	public async Task PreserveCancellationTokenFromAnOpenDriverOperation()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		using var connection = new RecordingConnection(true);

		var error = await Should.ThrowAsync<OperationCanceledException>(() => StaticDbCommand.ExecuteAsync(connection,
			new CommandDefinition("UPDATE t SET n=1", cancellationToken: cancellation.Token), static _ => { }));

		error.CancellationToken.ShouldBe(cancellation.Token);
		connection.Command.ObservedToken.ShouldBe(cancellation.Token);
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeReaderAndCommandOnMaterializationFailure()
	{
		using var table = new DataTable();
		_ = table.Columns.Add("Value", typeof(int));
		_ = table.Rows.Add(17);
		using var reader = table.CreateDataReader();
		using var connection = new RecordingConnection(false) { Reader = reader };

		_ = await Should.ThrowAsync<InvalidDataException>(() => StaticDbCommand.QueryAsync<int>(connection,
			new CommandDefinition("SELECT Value"), static _ => { }, static _ => throw new InvalidDataException("bad row")));

		reader.IsClosed.ShouldBeTrue();
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(1);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PreserveMaterializationFailureAndAttemptEveryOwnedCleanup(bool initiallyOpen, bool single)
	{
		var primary = new InvalidDataException("materialization primary");
		var readerFailure = new IOException("reader cleanup");
		var commandFailure = new IOException("command cleanup");
		var readerDisposeAttempted = false;
		using var reader = A.Fake<DbDataReader>();
		A.CallTo(() => reader.ReadAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
		A.CallTo(() => reader.FieldCount).Returns(1);
		A.CallTo(() => reader.DisposeAsync()).ReturnsLazily(() =>
		{
			readerDisposeAttempted = true;
			return ValueTask.FromException(readerFailure);
		});
		using var connection = new RecordingConnection(initiallyOpen)
		{
			Reader = reader,
			CommandDisposeFailure = commandFailure,
		};
		int Materialize(DbDataReader _) => throw primary;
		Exception? actual = null;
		try
		{
			if (single)
			{
				_ = await StaticDbCommand.SingleAsync(connection, new CommandDefinition("SELECT Value"), static _ => { }, Materialize);
			}
			else
			{
				_ = await StaticDbCommand.QueryAsync(connection, new CommandDefinition("SELECT Value"), static _ => { }, Materialize);
			}
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CommandDisposeFailure = null;
		}

		readerDisposeAttempted.ShouldBeTrue();
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.OpenCalls.ShouldBe(initiallyOpen ? 0 : 1);
		connection.CloseCalls.ShouldBe(initiallyOpen ? 0 : 1);
		connection.State.ShouldBe(initiallyOpen ? ConnectionState.Open : ConnectionState.Closed);
		actual.ShouldBeSameAs(primary, "cleanup faults must not replace the original materialization failure");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AttemptScalarCommandDisposalAfterCloseFailureAndPreserveTheFirstFailure(bool failOperation)
	{
		var primary = new InvalidDataException("scalar primary");
		var closeFailure = new IOException("connection close");
		var commandFailure = new IOException("command cleanup");
		using var connection = new RecordingConnection(false)
		{
			ScalarFailure = failOperation ? primary : null,
			CloseFailure = closeFailure,
			CommandDisposeFailure = commandFailure,
		};
		Exception? actual = null;
		try
		{
			_ = await StaticDbCommand.ScalarAsync(connection, new CommandDefinition("SELECT 17"), static _ => { });
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			// Test teardown must not issue another injected connection-close failure.
			connection.CloseFailure = null;
			connection.CommandDisposeFailure = null;
		}

		connection.OpenCalls.ShouldBe(1);
		connection.CloseCalls.ShouldBe(1);
		connection.Command.WasDisposed.ShouldBeTrue();
		actual.ShouldBeSameAs(failOperation ? primary : closeFailure,
			"the operation fault wins; without one, the first cleanup fault remains observable");
	}

	[Fact]
	public async Task PreserveBinderFailureWhenDisposingItsPartlyCreatedCommandAlsoFails()
	{
		var primary = new ArgumentException("binder primary");
		var cleanup = new IOException("command cleanup");
		using var connection = new RecordingConnection(false) { CommandDisposeFailure = cleanup };
		Exception? actual = null;
		try
		{
			_ = await StaticDbCommand.ExecuteAsync(connection, new CommandDefinition("UPDATE t SET n=1"), _ => throw primary);
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CommandDisposeFailure = null;
		}

		actual.ShouldBeSameAs(primary);
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.OpenCalls.ShouldBe(0);
		connection.CloseCalls.ShouldBe(0);
		var entries = (IReadOnlyList<KeyValuePair<string, Exception>>)primary.Data[CleanupKey]!;
		entries.Count.ShouldBe(1);
		entries[0].Key.ShouldBe("CommandCreateDispose");
		entries[0].Value.ShouldBeSameAs(cleanup);
	}

	[Fact]
	public async Task PreserveCancellationIdentityTokenAndCanceledTaskAcrossAllCleanupFailures()
	{
		using var cancellation = new CancellationTokenSource();
		var primary = new OperationCanceledException("materialization cancellation", cancellation.Token);
		var readerFailure = new IOException("reader cleanup");
		var closeFailure = new IOException("connection close");
		var commandFailure = new IOException("command cleanup");
		var readerDisposed = false;
		using var reader = A.Fake<DbDataReader>();
		A.CallTo(() => reader.ReadAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
		A.CallTo(() => reader.DisposeAsync()).ReturnsLazily(() =>
		{
			readerDisposed = true;
			return ValueTask.FromException(readerFailure);
		});
		using var connection = new RecordingConnection(false)
		{
			Reader = reader,
			CloseFailure = closeFailure,
			CommandDisposeFailure = commandFailure,
		};
		int Materialize(DbDataReader _)
		{
			cancellation.Cancel();
			throw primary;
		}

		var operation = StaticDbCommand.QueryAsync(connection,
			new CommandDefinition("SELECT Value", cancellationToken: cancellation.Token), static _ => { }, Materialize);
		Exception? actual = null;
		try
		{
			_ = await operation;
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CloseFailure = null;
			connection.CommandDisposeFailure = null;
		}

		actual.ShouldBeSameAs(primary);
		primary.CancellationToken.ShouldBe(cancellation.Token);
		operation.IsCanceled.ShouldBeTrue();
		operation.IsFaulted.ShouldBeFalse();
		connection.Command.ObservedToken.ShouldBe(cancellation.Token);
		readerDisposed.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(1);
		connection.Command.WasDisposed.ShouldBeTrue();
		var entries = (IReadOnlyList<KeyValuePair<string, Exception>>)primary.Data[CleanupKey]!;
		entries.Select(pair => pair.Key).ShouldBe(["ReaderDispose", "ConnectionClose", "CommandCreateOrDispose"]);
		entries[0].Value.ShouldBeSameAs(readerFailure);
		entries[1].Value.ShouldBeSameAs(closeFailure);
		entries[2].Value.ShouldBeSameAs(commandFailure);
	}

	[Fact]
	public async Task PreserveOutputProjectionFailureAndStillAttemptCloseAndCommandDisposal()
	{
		var primary = new InvalidDataException("output projection primary");
		var closeFailure = new IOException("connection close");
		var commandFailure = new IOException("command cleanup");
		using var connection = new RecordingConnection(false)
		{
			CloseFailure = closeFailure,
			CommandDisposeFailure = commandFailure,
		};
		var readBeforeDisposal = false;
		int ReadOutputs(DbCommand command)
		{
			readBeforeDisposal = ReferenceEquals(command, connection.Command) && !connection.Command.WasDisposed;
			throw primary;
		}

		Exception? actual = null;
		try
		{
			_ = await StaticDbCommand.ExecuteOutputsAsync(connection, new CommandDefinition("BEGIN outputs; END;"), static _ => { }, ReadOutputs);
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CloseFailure = null;
			connection.CommandDisposeFailure = null;
		}

		actual.ShouldBeSameAs(primary);
		readBeforeDisposal.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(1);
		connection.Command.WasDisposed.ShouldBeTrue();
		var entries = (IReadOnlyList<KeyValuePair<string, Exception>>)primary.Data[CleanupKey]!;
		entries.Select(pair => pair.Key).ShouldBe(["ConnectionClose", "CommandCreateOrDispose"]);
		entries[0].Value.ShouldBeSameAs(closeFailure);
		entries[1].Value.ShouldBeSameAs(commandFailure);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepPrimaryAndCleanupAttemptsWhenExceptionDataRejectsDiagnostics(bool readOnly)
	{
		IDictionary? data = readOnly ? new ReadOnlyDictionary<object, object>(new Dictionary<object, object>()) : null;
		var primary = new DiagnosticPrimaryException(data);
		var readerDisposed = false;
		using var reader = A.Fake<DbDataReader>();
		A.CallTo(() => reader.ReadAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
		A.CallTo(() => reader.DisposeAsync()).ReturnsLazily(() =>
		{
			readerDisposed = true;
			return ValueTask.FromException(new IOException("reader cleanup"));
		});
		using var connection = new RecordingConnection(false)
		{
			Reader = reader,
			CloseFailure = new IOException("connection close"),
			CommandDisposeFailure = new IOException("command cleanup"),
		};
		Exception? actual = null;
		try
		{
			_ = await StaticDbCommand.QueryAsync<int>(connection, new CommandDefinition("SELECT Value"), static _ => { }, _ => throw primary);
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CloseFailure = null;
			connection.CommandDisposeFailure = null;
		}

		actual.ShouldBeSameAs(primary);
		readerDisposed.ShouldBeTrue();
		connection.CloseCalls.ShouldBe(1);
		connection.Command.WasDisposed.ShouldBeTrue();
		if (readOnly)
		{
			data!.Count.ShouldBe(0);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task ExposeOrderedSecondaryFailuresWithoutReplacingCallerOwnedDiagnosticKeys(int occupiedKeys)
	{
		var primary = new InvalidDataException("materialization primary");
		var originalMain = new object();
		var originalAlternate = new object();
		if (occupiedKeys > 0)
		{
			primary.Data[CleanupKey] = originalMain;
		}

		if (occupiedKeys > 1)
		{
			primary.Data[AlternateCleanupKey] = originalAlternate;
		}

		var readerFailure = new IOException("reader cleanup");
		var commandFailure = new IOException("command cleanup");
		using var reader = A.Fake<DbDataReader>();
		A.CallTo(() => reader.ReadAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
		A.CallTo(() => reader.DisposeAsync()).ReturnsLazily(() => ValueTask.FromException(readerFailure));
		using var connection = new RecordingConnection(false) { Reader = reader, CommandDisposeFailure = commandFailure };
		Exception? actual = null;
		try
		{
			_ = await StaticDbCommand.QueryAsync<int>(connection, new CommandDefinition("SELECT Value"), static _ => { }, _ => throw primary);
		}
		catch (Exception error)
		{
			actual = error;
		}
		finally
		{
			connection.CommandDisposeFailure = null;
		}

		actual.ShouldBeSameAs(primary);
		connection.CloseCalls.ShouldBe(1);
		connection.Command.WasDisposed.ShouldBeTrue();
		if (occupiedKeys > 0)
		{
			primary.Data[CleanupKey].ShouldBeSameAs(originalMain);
		}

		if (occupiedKeys > 1)
		{
			primary.Data[AlternateCleanupKey].ShouldBeSameAs(originalAlternate);
			primary.Data.Count.ShouldBe(2);
		}
		else
		{
			var key = occupiedKeys == 0 ? CleanupKey : AlternateCleanupKey;
			var entries = (IReadOnlyList<KeyValuePair<string, Exception>>)primary.Data[key]!;
			entries.Select(pair => pair.Key).ShouldBe(["ReaderDispose", "CommandCreateOrDispose"]);
			entries[0].Value.ShouldBeSameAs(readerFailure);
			entries[1].Value.ShouldBeSameAs(commandFailure);
		}
	}


	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExposeAdoThroughDataRequestWithCurrentInputsAndOriginalCommandAuthority(bool initiallyOpen)
	{
		using var connection = new RecordingConnection(initiallyOpen);
		using var transaction = connection.BeginTransaction();
		using var cancellation = new CancellationTokenSource();
		var request = new NonQueryBridgeRequest(transaction, cancellation.Token);
		request.Parameters.Add("Value", 29, DbType.Int32);
		(await request.ResolveAsync(connection)).ShouldBe(3);
		request.BoundValue.ShouldBe(29);
		connection.Command.CommandText.ShouldBe("KnownOperation");
		connection.Command.CommandType.ShouldBe(CommandType.StoredProcedure);
		connection.Command.CommandTimeout.ShouldBe(47);
		connection.Command.Transaction.ShouldBeSameAs(transaction);
		connection.Command.ObservedToken.ShouldBe(cancellation.Token);
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.State.ShouldBe(initiallyOpen ? ConnectionState.Open : ConnectionState.Closed);
		connection.OpenCalls.ShouldBe(initiallyOpen ? 0 : 1);
		connection.CloseCalls.ShouldBe(initiallyOpen ? 0 : 1);
	}

	[Fact]
	public async Task PreservePrimaryCancellationThroughTheDataRequestScalarBridge()
	{
		using var cancellation = new CancellationTokenSource();
		var primary = new OperationCanceledException("driver cancellation", cancellation.Token);
		using var connection = new RecordingConnection(true)
		{
			ScalarFailure = primary,
			CommandDisposeFailure = new IOException("command cleanup failed"),
		};
		var request = new ScalarBridgeRequest(cancellation.Token);
		OperationCanceledException? caught = null;
		try
		{
			_ = await request.ResolveAsync(connection);
		}
		catch (OperationCanceledException error)
		{
			caught = error;
		}
		caught.ShouldNotBeNull().ShouldBeSameAs(primary);
		caught.CancellationToken.ShouldBe(cancellation.Token);
		connection.Command.ObservedToken.ShouldBe(cancellation.Token);
		connection.State.ShouldBe(ConnectionState.Open);
		connection.CommandDisposeFailure = null;
	}

	[Fact]
	public async Task BufferExplicitDataRequestRowsBeforeClosingOwnedReadResources()
	{
		using var table = new DataTable();
		_ = table.Columns.Add("Value", typeof(int));
		_ = table.Rows.Add(17);
		_ = table.Rows.Add(23);
		using var reader = table.CreateDataReader();
		using var connection = new RecordingConnection(false) { Reader = reader };
		using var cancellation = new CancellationTokenSource();
		var request = new RowsBridgeRequest(cancellation.Token);
		var rows = await request.ResolveAsync(connection);
		reader.IsClosed.ShouldBeTrue();
		connection.Command.WasDisposed.ShouldBeTrue();
		connection.State.ShouldBe(ConnectionState.Closed);
		connection.Command.ObservedToken.ShouldBe(cancellation.Token);
		rows.ShouldBe([17, 23]);
	}

	private sealed class NonQueryBridgeRequest : DataRequestBase<IDbConnection, int>
	{
		internal NonQueryBridgeRequest(IDbTransaction transaction, CancellationToken token)
		{
			Parameters.Add("Value", 1, DbType.Int32);
			Command = CreateCommand("KnownOperation", transaction: transaction, commandTimeout: 47,
				commandType: CommandType.StoredProcedure, cancellationToken: token);
			ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
			{
				BoundValue = Parameters.Get<int>("Value");
				var parameter = command.CreateParameter();
				parameter.ParameterName = "Value";
				parameter.DbType = DbType.Int32;
				parameter.Value = BoundValue;
				_ = command.Parameters.Add(parameter);
			});
		}

		internal int BoundValue { get; private set; }
	}

	private sealed class ScalarBridgeRequest : DataRequestBase<IDbConnection, object?>
	{
		internal ScalarBridgeRequest(CancellationToken token)
		{
			Command = CreateCommand("SELECT Value", cancellationToken: token);
			ResolveAsync = connection => ExecuteScalarAsync(connection, static _ => { });
		}
	}

	private sealed class RowsBridgeRequest : DataRequestBase<IDbConnection, IEnumerable<int>>
	{
		internal RowsBridgeRequest(CancellationToken token)
		{
			Command = CreateCommand("SELECT Value", cancellationToken: token);
			ResolveAsync = connection => ReadRowsAsync(connection, static _ => { }, static row => row.GetInt32(0));
		}
	}

	private sealed class DiagnosticPrimaryException(IDictionary? data) : Exception("primary with non-writable diagnostics")
	{
		public override IDictionary Data => data ?? throw new NotSupportedException("diagnostic data getter unavailable");
	}

	private sealed class RecordingConnection(bool open) : DbConnection
	{
		private ConnectionState _state = open ? ConnectionState.Open : ConnectionState.Closed;
		internal RecordingCommand Command { get; private set; } = null!;
		internal int OpenCalls { get; private set; }
		internal int CloseCalls { get; private set; }
		internal bool FailExecution { get; init; }
		internal DbDataReader? Reader { get; init; }
		internal Exception? ScalarFailure { get; init; }
		internal Exception? CloseFailure { get; set; }
		internal Exception? CommandDisposeFailure { get; set; }
		[AllowNull] public override string ConnectionString { get; set; } = "";
		public override string Database => "test";
		public override string DataSource => "test";
		public override string ServerVersion => "1";
		public override ConnectionState State => _state;
		public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
		public override void Open() { OpenCalls++; _state = ConnectionState.Open; }
		public override Task OpenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
		public override void Close()
		{
			CloseCalls++;
			if (CloseFailure is { } error)
			{
				throw error;
			}

			_state = ConnectionState.Closed;
		}
		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new RecordingTransaction(this, isolationLevel);
		protected override DbCommand CreateDbCommand() => Command = new RecordingCommand(this);
	}

	private sealed class RecordingTransaction(DbConnection connection, IsolationLevel isolation) : DbTransaction
	{
		public override IsolationLevel IsolationLevel => isolation;
		protected override DbConnection DbConnection => connection;
		public override void Commit() => throw new NotSupportedException();
		public override void Rollback() => throw new NotSupportedException();
	}

	private sealed class RecordingCommand(RecordingConnection owner) : DbCommand
	{
		internal bool WasDisposed { get; private set; }
		internal CancellationToken ObservedToken { get; private set; }
		internal CommandBehavior Behavior { get; private set; }
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; } = 30;
		public override CommandType CommandType { get; set; } = CommandType.Text;
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; } = owner;
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection { get; } = A.Fake<DbParameterCollection>();
		protected override DbParameter CreateDbParameter() => A.Fake<DbParameter>();
		public override void Cancel() { }
		public override void Prepare() { }
		public override object? ExecuteScalar() => throw new NotSupportedException();
		public override Task<object?> ExecuteScalarAsync(CancellationToken token)
		{
			Observe(token);
			if (owner.ScalarFailure is { } error)
			{
				throw error;
			}

			return Task.FromResult<object?>(17);
		}
		public override int ExecuteNonQuery() => throw new NotSupportedException();
		public override Task<int> ExecuteNonQueryAsync(CancellationToken token)
		{
			Observe(token);
			return Task.FromResult(3);
		}
		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken token)
		{
			Behavior = behavior;
			Observe(token);
			return Task.FromResult(owner.Reader ?? throw new InvalidOperationException("No reader configured."));
		}
		private void Observe(CancellationToken token)
		{
			ObservedToken = token;
			token.ThrowIfCancellationRequested();
			if (owner.FailExecution) { throw new IOException("injected driver failure"); }
		}
		protected override void Dispose(bool disposing)
		{
			WasDisposed = true;
			if (owner.CommandDisposeFailure is { } error)
			{
				throw error;
			}

			base.Dispose(disposing);
		}
	}

	private sealed class OpenConnectionWrapper(RecordingConnection inner) : IDbConnection
	{
		[AllowNull] public string ConnectionString { get => inner.ConnectionString; set => inner.ConnectionString = value; }
		public int ConnectionTimeout => inner.ConnectionTimeout;
		public string Database => inner.Database;
		public ConnectionState State => inner.State;
		public IDbTransaction BeginTransaction() => inner.BeginTransaction();
		public IDbTransaction BeginTransaction(IsolationLevel level) => inner.BeginTransaction(level);
		public void ChangeDatabase(string databaseName) => inner.ChangeDatabase(databaseName);
		public void Close() => inner.Close();
		public IDbCommand CreateCommand() => inner.CreateCommand();
		public void Open() => inner.Open();
		public void Dispose() { }
	}
}

[CollectionDefinition("Static database command settings", DisableParallelization = true)]
public sealed class StaticDatabaseCommandSettingsCollection;
