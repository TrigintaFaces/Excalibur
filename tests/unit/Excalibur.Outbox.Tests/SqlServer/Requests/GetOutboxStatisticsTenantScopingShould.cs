// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Binds the outbox statistics read as an estate-wide operator report, carrying no tenant term.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file previously asserted the opposite</b>, and the reversal is the point. It argued that the
/// statistics read was the one statement whose tenant term was its only isolation, and that removing the
/// term would turn a liveness bug into a cross-tenant disclosure. That reading does not survive the
/// signature: the store method that reaches this request takes no tenant argument, and the statistics type
/// it returns carries no tenant field, so a confined result has no way to say which partition it describes.
/// Confinement here is not underspecified — it is unrepresentable.
/// </para>
/// <para>
/// <b>The mechanism was the decisive objection.</b> An outbox store reads no ambient tenant context. The
/// only way this statement could obtain a partition was to infer one from ambient state, which is precisely
/// what the admin contract these operations live on forbids in writing, one declaration above this one.
/// The other relational providers have declared this read estate-wide all along.
/// </para>
/// <para>
/// <b>Arms are paired.</b> The safety arm proves no tenant term survives; the liveness arm proves the
/// statement still aggregates over the table. A request that emitted nothing at all would satisfy the
/// safety arm on its own, which is the failure this pairing exists to exclude.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GetOutboxStatisticsTenantScopingShould : UnitTestBase
{
	private const string OutboxTable = "[dbo].[OutboxMessages]";

	private static readonly char[] NewlineChars = ['\r', '\n'];

	private static CommandDefinition Build() =>
		new GetOutboxStatisticsRequest(OutboxTable, 30, CancellationToken.None).Command;

	/// <summary>
	/// The executable statement, with <c>--</c> comment lines removed. The SQL documents its own lease-based
	/// in-flight count in prose that contains the word <c>WHERE</c>, so a bare text search for a clause would
	/// match the explanation rather than the statement — matching the commentary is not evidence about the
	/// predicate the server receives.
	/// </summary>
	private static string ExecutableSql() =>
		string.Join(
			Environment.NewLine,
			Build().CommandText
				.Split(NewlineChars)
				.Where(static line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

	/// <summary>
	/// SAFETY. No tenant predicate reaches the server. The counters describe the whole table, so a term
	/// naming one partition would silently narrow an operator's report to a fraction of the estate.
	/// </summary>
	[Fact]
	public void CarryNoTenantPredicate()
	{
		var sql = Build().CommandText;

		sql.ShouldNotContain(
			"TenantId",
			Case.Insensitive,
			customMessage: "the statistics read is an estate-wide operator report reached through a method "
				+ "that takes no tenant argument; any tenant term here narrows it to a partition the caller "
				+ "never named and the result type cannot identify");
	}

	/// <summary>
	/// SAFETY. No tenant value is bound either. A binding whose predicate is gone is inert, and it implies
	/// a constraint the statement no longer carries.
	/// </summary>
	[Fact]
	public void BindNoTenantParameter()
	{
		var parameters = Build().Parameters as DynamicParameters;
		_ = parameters.ShouldNotBeNull("the request must supply Dapper parameters for this lock to read");

		parameters.ParameterNames.ShouldNotContain(
			name => name.Contains("Tenant", StringComparison.OrdinalIgnoreCase),
			customMessage: "a bound tenant value with no predicate to consume it is inert and misleads the "
				+ "next reader into believing the statement is confined");
	}

	/// <summary>
	/// LIVENESS. The statement still aggregates over the outbox table. Without this arm, a request that
	/// emitted empty or table-less SQL would pass both safety arms above.
	/// </summary>
	[Fact]
	public void StillAggregateOverTheWholeOutboxTable()
	{
		var sql = Build().CommandText;

		sql.ShouldContain(
			OutboxTable,
			customMessage: "a statement that no longer names the outbox table counts nothing, which passes "
				+ "every safety arm here while reporting zero for the entire estate");
		sql.ShouldContain("SUM(", customMessage: "the report is built from aggregate counters");

		ExecutableSql().ShouldNotContain(
			"WHERE",
			Case.Insensitive,
			customMessage: "the aggregate admits every row; any WHERE clause bounds a count the operator "
				+ "reads as estate-wide, whatever column it names");
	}
}
