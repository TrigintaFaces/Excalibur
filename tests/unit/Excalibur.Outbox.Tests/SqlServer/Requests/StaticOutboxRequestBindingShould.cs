// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Outbox.SqlServer.Requests;

using FakeItEasy;

using Microsoft.Data.SqlClient;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class StaticOutboxRequestBindingShould
{
	[Fact]
	public async Task ExecuteCurrentKnownParameterValuesRatherThanCapturedConstructorValues()
	{
		using var command = new RecordingCommand();
		using var connection = Connection(command);
		var request = Request();
		request.Parameters.Add("BatchSize", 3);
		request.Parameters.Add("ProcessorId", "replacement:claim");

		(await request.ResolveAsync(connection)).ShouldBeEmpty();

		command.Values["BatchSize"].ShouldBe(3);
		command.Values["ProcessorId"].ShouldBe("replacement:claim");
		command.Types["BatchSize"].ShouldBe(DbType.Int32);
		command.Types["ProcessorId"].ShouldBe(DbType.String);
		command.Sizes["ProcessorId"].ShouldBe(4000);
		command.Values["FencingToken"].ShouldBe(DBNull.Value);
	}

	[Fact]
	public async Task RejectAnInvalidKnownValueRepresentationAndDisposeTheOwnedCommand()
	{
		using var command = new RecordingCommand();
		using var connection = Connection(command);
		var request = Request();
		request.Parameters.Add("BatchSize", "not an integer");

		var error = await Should.ThrowAsync<ArgumentException>(() => request.ResolveAsync(connection));

		error.ParamName.ShouldBe("BatchSize");
		error.InnerException.ShouldBeOfType<InvalidCastException>();
		command.Executed.ShouldBeFalse();
		command.WasDisposed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectANullNonnullableNumericInputWithItsName(bool dbNull)
	{
		using var command = new RecordingCommand();
		using var connection = Connection(command);
		var request = Request();
		request.Parameters.Add("BatchSize", dbNull ? DBNull.Value : null);

		var error = await Should.ThrowAsync<ArgumentException>(() => request.ResolveAsync(connection));

		error.ParamName.ShouldBe("BatchSize");
		command.Executed.ShouldBeFalse();
		command.WasDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectANullRequiredInputInsteadOfSendingAnUnownedClaim()
	{
		using var command = new RecordingCommand();
		using var connection = Connection(command);
		var request = Request();
		request.Parameters.Add("ProcessorId", null);

		var error = await Should.ThrowAsync<ArgumentException>(() => request.ResolveAsync(connection));

		error.ParamName.ShouldBe("ProcessorId");
		command.Executed.ShouldBeFalse();
		command.WasDisposed.ShouldBeTrue();
	}

	private static GetUnsentMessagesRequest Request() => new("dbo.Outbox", 17, 47, 300, "processor", null, "dbo.Fence", "dbo.Outbox", CancellationToken.None);

	private static IDbConnection Connection(DbCommand command)
	{
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		_ = A.CallTo(() => connection.CreateCommand()).Returns(command);
		return connection;
	}

	private sealed class RecordingCommand : DbCommand
	{
		private readonly SqlCommand _parameters = new();
		internal Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
		internal Dictionary<string, DbType> Types { get; } = new(StringComparer.Ordinal);
		internal Dictionary<string, int> Sizes { get; } = new(StringComparer.Ordinal);
		internal bool WasDisposed { get; private set; }
		internal bool Executed { get; private set; }
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; }
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; }
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;
		protected override DbParameter CreateDbParameter() => new SqlParameter();
		public override void Cancel() { }
		public override void Prepare() { }
		public override object? ExecuteScalar() => throw new NotSupportedException();
		public override int ExecuteNonQuery() => throw new NotSupportedException();
		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken token)
		{
			Executed = true;
			token.ThrowIfCancellationRequested();
			foreach (DbParameter parameter in Parameters)
			{
				Values[parameter.ParameterName] = parameter.Value!;
				Types[parameter.ParameterName] = parameter.DbType;
				Sizes[parameter.ParameterName] = parameter.Size;
			}

			return Task.FromResult<DbDataReader>(new DataTable().CreateDataReader());
		}
		protected override void Dispose(bool disposing)
		{
			WasDisposed = true;
			if (disposing) { _parameters.Dispose(); }
			base.Dispose(disposing);
		}
	}
}
