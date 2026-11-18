// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Dapper;
using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Requests;
using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.DataProcessing.Requests;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class DataTaskCommandLifecycleShould
{
    [Fact]
    public async Task PreserveInsertMetadataAndExecutedValuesAcrossRepeatedExecution()
    {
        using var connection = new RecordingConnection(open: true);
        var id = Guid.NewGuid();
        var request = new InsertDataTask(id, "雪-record", new DataProcessingOptions { MaxAttempts = 9 }, 43, CancellationToken.None);
        request.Command.Parameters.ShouldBeSameAs(request.Parameters);
        var created = request.Parameters.Get<DateTimeOffset>("CreatedAt");
        (await request.ResolveAsync(connection)).ShouldBe(3);
        (await request.ResolveAsync(connection)).ShouldBe(3);
        connection.Commands.Count.ShouldBe(2);
        foreach (var command in connection.Commands)
        {
            command.CommandText.ShouldBe(request.Command.CommandText);
            command.CommandTimeout.ShouldBe(43);
            command.Values["DataTaskId"].ShouldBe(id);
            command.Values["CreatedAt"].ShouldBe(created);
            command.Values["RecordType"].ShouldBe("雪-record");
            command.Values["Attempts"].ShouldBe(0);
            command.Values["MaxAttempts"].ShouldBe(9);
            command.Types["DataTaskId"].ShouldBe(DbType.Guid);
            command.Types["CreatedAt"].ShouldBe(DbType.DateTimeOffset);
            command.Types["Attempts"].ShouldBe(DbType.Int32);
            command.WasDisposed.ShouldBeTrue();
        }
        request.Parameters.Get<DateTimeOffset>("CreatedAt").ShouldBe(created);
        connection.CloseCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RestoreOnlyConnectionsOpenedByTheRequest(bool initiallyOpen)
    {
        using var connection = new RecordingConnection(initiallyOpen);
        await new DeleteDataTask(Guid.NewGuid(), new DataProcessingOptions(), 31, CancellationToken.None).ResolveAsync(connection);
        connection.OpenCalls.ShouldBe(initiallyOpen ? 0 : 1);
        connection.CloseCalls.ShouldBe(initiallyOpen ? 0 : 1);
        connection.State.ShouldBe(initiallyOpen ? ConnectionState.Open : ConnectionState.Closed);
    }

    [Fact]
    public async Task AcceptAnAlreadyOpenIdbConnectionWrapper()
    {
        using var inner = new RecordingConnection(open: true);
        using var wrapper = new OpenConnectionWrapper(inner);
        (await new UpdateDataTaskAttempts(Guid.NewGuid(), 4, new DataProcessingOptions(), 17, CancellationToken.None).ResolveAsync(wrapper)).ShouldBe(3);
        inner.Commands.Single().Values["Attempts"].ShouldBe(4);
        inner.OpenCalls.ShouldBe(0);
        inner.CloseCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeCommandsAndRestoreOwnershipOnExecutionFailureOrCancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        if (cancel) { cancellation.Cancel(); }
        using var connection = new RecordingConnection(open: false) { FailExecution = !cancel };
        var request = new DeleteDataTask(Guid.NewGuid(), new DataProcessingOptions(), 31, cancellation.Token);
        if (cancel) { await Should.ThrowAsync<OperationCanceledException>(() => request.ResolveAsync(connection)); }
        else { await Should.ThrowAsync<InvalidOperationException>(() => request.ResolveAsync(connection)); }
        connection.Commands.Single().WasDisposed.ShouldBeTrue();
        connection.State.ShouldBe(ConnectionState.Closed);
        if (!cancel) { connection.CloseCalls.ShouldBe(1); }
    }

    [Fact]
    public async Task PassCancellationToExecutionWithoutClosingAnAlreadyOpenConnection()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var connection = new RecordingConnection(open: true);
        var request = new DeleteDataTask(Guid.NewGuid(), new DataProcessingOptions(), 31, cancellation.Token);
        await Should.ThrowAsync<OperationCanceledException>(() => request.ResolveAsync(connection));
        connection.Commands.Single().ObservedToken.ShouldBe(cancellation.Token);
        connection.Commands.Single().WasDisposed.ShouldBeTrue();
        connection.CloseCalls.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task DisposeACommandWhenStaticParameterBindingFails()
    {
        using var connection = new RecordingConnection(open: false);
        var error = new InvalidOperationException("injected binding failure");
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => DataTaskCommandExecutor.ExecuteAsync(
            connection, new CommandDefinition("SELECT 1"), _ => throw error));
        actual.ShouldBeSameAs(error);
        connection.Commands.Single().WasDisposed.ShouldBeTrue();
        connection.OpenCalls.ShouldBe(0);
        connection.CloseCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("雪-α")]
    [InlineData("")]
    public async Task PreserveUnicodeNullAndEmptyCursorValues(string? cursor)
    {
        using var connection = new RecordingConnection(open: true);
        var id = Guid.NewGuid();
        var request = new UpdateDataTaskCompletedCount(id, long.MaxValue, cursor, new DataProcessingOptions(), 39, CancellationToken.None);
        await request.ResolveAsync(connection);
        var command = connection.Commands.Single();
        command.Values["DataTaskId"].ShouldBe(id);
        command.Values["CompletedCount"].ShouldBe(long.MaxValue);
        command.Values["ProcessedCursor"].ShouldBe(cursor is null ? DBNull.Value : cursor);
        command.CommandText.ShouldContain("COALESCE(@ProcessedCursor, ProcessedCursor)");
        if (cursor is not null) { command.Sizes["ProcessedCursor"].ShouldBe(4000); }
    }

    [Fact]
    public async Task RetainProviderDefaultSizingForLongUnicodeCursor()
    {
        var cursor = new string('雪', 5001);
        using var connection = new RecordingConnection(open: true);
        await new UpdateDataTaskCompletedCount(Guid.NewGuid(), 8, cursor, new DataProcessingOptions(), 30, CancellationToken.None).ResolveAsync(connection);
        var command = connection.Commands.Single();
        command.Values["ProcessedCursor"].ShouldBe(cursor);
        command.Sizes["ProcessedCursor"].ShouldBe(new SqlParameter { Value = cursor }.Size);
        command.Types["ProcessedCursor"].ShouldBe(DbType.String);
    }

    [Fact]
    public async Task ApplyTimeoutTransactionAndCancellationFromCommandMetadata()
    {
        using var connection = new RecordingConnection(open: true);
        using var transaction = connection.BeginTransaction();
        using var cancellation = new CancellationTokenSource();
        var definition = new CommandDefinition("UPDATE tasks SET Attempts=1", transaction: transaction, commandTimeout: 71, cancellationToken: cancellation.Token);
        await DataTaskCommandExecutor.ExecuteAsync(connection, definition, static _ => { });
        var command = connection.Commands.Single();
        command.Transaction.ShouldBeSameAs(transaction);
        command.CommandTimeout.ShouldBe(71);
        command.ObservedToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeBufferedReadersAndCommandsAfterMappingSuccessOrFailure(bool badRecordType)
    {
        using var connection = new RecordingConnection(open: false);
        using var table = new DataTable();
        table.Columns.Add("DataTaskId", typeof(Guid));
        table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        table.Columns.Add("RecordType", badRecordType ? typeof(int) : typeof(string));
        table.Columns.Add("Attempts", typeof(int));
        table.Columns.Add("MaxAttempts", typeof(int));
        table.Columns.Add("CompletedCount", typeof(long));
        table.Columns.Add("FetchCursor", typeof(string));
        table.Columns.Add("ProcessedCursor", typeof(string));
        var id = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        table.Rows.Add(id, at, badRecordType ? (object)42 : "雪-record", 2, 9, 123L, DBNull.Value, "resume-α");
        table.Rows.Add(Guid.NewGuid(), at, badRecordType ? (object)43 : "second", 0, 9, 0L, "fetch", DBNull.Value);
        var reader = table.CreateDataReader();
        connection.Reader = reader;
        var request = new SelectPendingDataTasks(new DataProcessingOptions(), 53, CancellationToken.None);
        if (badRecordType)
        {
            await Should.ThrowAsync<InvalidCastException>(() => request.ResolveAsync(connection));
        }
        else
        {
            var tasks = (await request.ResolveAsync(connection)).ToArray();
            tasks.Length.ShouldBe(2);
            tasks[0].DataTaskId.ShouldBe(id);
            tasks[0].CreatedAt.ShouldBe(at);
            tasks[0].RecordType.ShouldBe("雪-record");
            tasks[0].Attempts.ShouldBe(2);
            tasks[0].MaxAttempts.ShouldBe(9);
            tasks[0].CompletedCount.ShouldBe(123);
            tasks[0].FetchCursor.ShouldBeNull();
            tasks[0].ProcessedCursor.ShouldBe("resume-α");
            tasks[1].ProcessedCursor.ShouldBeNull();
        }
        reader.IsClosed.ShouldBeTrue();
        connection.Commands.Single().WasDisposed.ShouldBeTrue();
        connection.Commands.Single().CommandTimeout.ShouldBe(53);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    private sealed class RecordingConnection(bool open) : DbConnection
    {
        private ConnectionState _state = open ? ConnectionState.Open : ConnectionState.Closed;
        internal List<RecordingCommand> Commands { get; } = [];
        internal int OpenCalls { get; private set; }
        internal int CloseCalls { get; private set; }
        internal bool FailExecution { get; init; }
        internal DbDataReader? Reader { get; set; }
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "test";
        public override string DataSource => "test";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Open() { OpenCalls++; _state = ConnectionState.Open; }
        public override Task OpenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
        public override void Close() { CloseCalls++; _state = ConnectionState.Closed; }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new RecordingTransaction(this, isolationLevel);
        protected override DbCommand CreateDbCommand()
        {
            var command = new RecordingCommand(this);
            Commands.Add(command);
            return command;
        }
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
        private readonly SqlCommand _parameterOwner = new();
        internal bool WasDisposed { get; private set; }
        internal CancellationToken ObservedToken { get; private set; }
        internal Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, int> Sizes { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, DbType> Types { get; } = new(StringComparer.Ordinal);
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = owner;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameterOwner.Parameters;
        protected override DbParameter CreateDbParameter() => new SqlParameter();
        public override void Cancel() { }
        public override void Prepare() { }
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException("Use async execution.");
        public override Task<int> ExecuteNonQueryAsync(CancellationToken token)
        {
            Observe(token);
            return Task.FromResult(3);
        }
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken token)
        {
            Observe(token);
            return Task.FromResult(owner.Reader ?? throw new InvalidOperationException("No reader configured."));
        }
        private void Observe(CancellationToken token)
        {
            ObservedToken = token;
            token.ThrowIfCancellationRequested();
            foreach (DbParameter parameter in Parameters)
            {
                Values[parameter.ParameterName] = parameter.Value!;
                Sizes[parameter.ParameterName] = parameter.Size;
                Types[parameter.ParameterName] = parameter.DbType;
            }
            if (owner.FailExecution) { throw new InvalidOperationException("injected execution failure"); }
        }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            if (disposing) { _parameterOwner.Dispose(); }
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
