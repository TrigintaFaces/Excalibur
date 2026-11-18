// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Outbox.SqlServer.Requests;

using FakeItEasy;

using Microsoft.Data.SqlClient;

using Shouldly;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
[Collection("SQL outbox list settings")]
public sealed class SqlServerOutboxIdListShould
{
	public static TheoryData<int, bool> Boundaries()
	{
		var cases = new TheoryData<int, bool>();
		foreach (var count in new[] { 0, 1, 5, 6, 149, 150, 151, 749, 750, 751, 1999, 2000, 2001, 2069, 2070, 2071, 2100, 2101 })
		{
			cases.Add(count, false); cases.Add(count, true);
		}
		return cases;
	}

	[Theory]
	[MemberData(nameof(Boundaries))]
	public Task MatchOriginalDapperExpansionAtEveryPaddingBoundary(int count, bool padded) =>
		CompareAsync(Enumerable.Range(0, count).Select(index => "id-" + index).ToArray(), padded);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveDuplicateNullAndLongStringItems(bool padded)
	{
		await CompareAsync(["same", "same", null!, "other"], padded);
		await CompareAsync(["first", "two", "three", "four", "five", new string('x', 4001)], padded);
		await CompareAsync(["first", "two", "three", "four", new string('x', 4001), null!], padded);
		await CompareAsync([new string('x', 4000)], padded);
	}

	[Fact]
	public async Task RejectTheSameInvalidFirstNullAsOriginalDapper()
	{
		var ids = new string[] { null!, "second" };
		using var original = new RecordingCommand();
		using var connection = Connection(original);
		_ = await Should.ThrowAsync<NotSupportedException>(() => connection.ExecuteAsync(new CommandDefinition(Sql, new { Ids = ids })));
		using var candidate = new RecordingCommand { CommandText = Sql };
		_ = Should.Throw<NotSupportedException>(() => SqlServerOutboxIdList.Bind(candidate, ids));
	}

	private const string Sql = "UPDATE dbo.Outbox SET Status=2 WHERE Id IN @Ids";

	private static async Task CompareAsync(IReadOnlyList<string> ids, bool padded)
	{
		var previous = SqlMapper.Settings.PadListExpansions;
		try
		{
			SqlMapper.Settings.PadListExpansions = padded;
			using var original = new RecordingCommand();
			using var connection = Connection(original);
			_ = await connection.ExecuteAsync(new CommandDefinition(Sql, new { Ids = ids }));
			using var candidate = new RecordingCommand { CommandText = Sql };
			SqlServerOutboxIdList.Bind(candidate, ids);
			_ = await candidate.ExecuteNonQueryAsync(CancellationToken.None);

			candidate.CommandText.ShouldBe(original.CommandText);
			candidate.Values.Count.ShouldBe(original.Values.Count);
			foreach (var (name, expected) in original.Values)
			{
				candidate.Values[name].ShouldBe(expected);
				candidate.Types[name].ShouldBe(original.Types[name]);
				candidate.Sizes[name].ShouldBe(original.Sizes[name]);
			}
		}
		finally { SqlMapper.Settings.PadListExpansions = previous; }
	}

	private static IDbConnection Connection(DbCommand command)
	{
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		_ = A.CallTo(() => connection.CreateCommand()).Returns(command);
		return connection;
	}

	private sealed class RecordingCommand : DbCommand
	{
		private readonly SqlCommand _parameterOwner = new();
		internal Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);
		internal Dictionary<string, DbType> Types { get; } = new(StringComparer.Ordinal);
		internal Dictionary<string, int> Sizes { get; } = new(StringComparer.Ordinal);
		[AllowNull] public override string CommandText { get; set; } = "";
		public override int CommandTimeout { get; set; }
		public override CommandType CommandType { get; set; }
		public override bool DesignTimeVisible { get; set; }
		public override UpdateRowSource UpdatedRowSource { get; set; }
		protected override DbConnection? DbConnection { get; set; }
		protected override DbTransaction? DbTransaction { get; set; }
		protected override DbParameterCollection DbParameterCollection => _parameterOwner.Parameters;
		protected override DbParameter CreateDbParameter() => new SqlParameter();
		public override void Cancel() { }
		public override void Prepare() { }
		public override int ExecuteNonQuery() => throw new NotSupportedException();
		public override object? ExecuteScalar() => throw new NotSupportedException();
		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
		public override Task<int> ExecuteNonQueryAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			foreach (DbParameter parameter in Parameters)
			{
				Values[parameter.ParameterName] = parameter.Value;
				Types[parameter.ParameterName] = parameter.DbType;
				Sizes[parameter.ParameterName] = parameter.Size;
			}
			return Task.FromResult(1);
		}
		protected override void Dispose(bool disposing) { if (disposing) { _parameterOwner.Dispose(); } base.Dispose(disposing); }
	}
}

[CollectionDefinition("SQL outbox list settings", DisableParallelization = true)]
public sealed class SqlOutboxListSettingsCollection;
