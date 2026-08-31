// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

using Tests.Shared.Helpers;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Locks the relationship between the SQL <see cref="MarkMessageFailedRequest"/> emits and the parameters it
/// binds, on every branch of its backoff argument.
/// </summary>
/// <remarks>
/// <para>
/// The sibling fixture asserts the command's shape — its text, its timeout, its resolver — and passed while a
/// parameter was bound inside one branch alone. Constructing a request never executes it, so a mismatch between
/// the SQL and the parameter set is invisible until the statement reaches a server. These tests close that gap
/// by comparing the two halves the request itself owns.
/// </para>
/// <para>
/// <b>The property is bidirectional, and the second direction is why this file survived the tenant-term
/// removal.</b> A parameter the SQL names but the command does not carry fails at the server. A parameter the
/// command carries but no SQL names is dead weight — and it is the specific residue of DELETING a predicate:
/// the <c>WHERE</c> fragment goes and the <c>parameters.Add</c> stays. Neither direction detects the other, so
/// both arms are here.
/// </para>
/// <para>
/// <b>This class covers the backoff BRANCHES;</b> the sibling
/// <see cref="OutboxRequestParameterBindingShould"/> covers the same property across every request type in the
/// package. Only this class can reach all three branches, because only this request has them — which is
/// exactly where the original defect lived.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MarkMessageFailedRequestParameterBindingShould : UnitTestBase
{
	private const string TableName = "[dbo].[OutboxMessages]";
	private const string MessageId = "msg-12345";
	private const string ErrorMessage = "Connection timeout";
	private const string LeasedBy = "processor-1";

	private static MarkMessageFailedRequest Build(DateTimeOffset? nextAttemptAt, int? floorSeconds) =>
		new(
			TableName,
			MessageId,
			ErrorMessage,
			1,
			LeasedBy,
			30,
			CancellationToken.None,
			nextAttemptAt,
			floorSeconds);

	public static TheoryData<string, bool, int?> BackoffPaths => new()
	{
		// name                       usesNextAttemptAt  floorSeconds
		{ "neither argument", false, null },
		{ "explicit next-attempt time", true, null },
		{ "failure-anchored floor", false, 60 },
	};

	/// <summary>
	/// LIVENESS. On every backoff path the command carries a value for each parameter its SQL names, so the
	/// statement can execute at all. This is the arm that fails when a parameter is bound inside one branch:
	/// the two other paths emit SQL naming parameters the command never carries.
	/// </summary>
	[Theory]
	[MemberData(nameof(BackoffPaths))]
	public void BindEveryParameterItsSqlReferences(string path, bool usesNextAttemptAt, int? floorSeconds)
	{
		var request = Build(usesNextAttemptAt ? DateTimeOffset.UtcNow.AddMinutes(5) : null, floorSeconds);

		var referenced = SqlParameterTokens.ReferencedBy(request.Command.CommandText);
		var bound = SqlParameterTokens.BoundBy(request.Parameters);

		referenced.ShouldNotBeEmpty($"the {path} path should emit a parameterised statement");

		var unbound = referenced.Except(bound, StringComparer.OrdinalIgnoreCase).ToList();
		unbound.ShouldBeEmpty(
			$"the {path} path emits SQL referencing {SqlParameterTokens.Format(unbound)} "
			+ "which the command does not carry, so the statement fails at the server");
	}

	/// <summary>
	/// SAFETY. On every backoff path every parameter the command carries is named by its SQL. A bound
	/// parameter no statement references is the residue of a deleted predicate: the fragment was removed and
	/// the binding was left behind, which is silent and cumulative.
	/// </summary>
	[Theory]
	[MemberData(nameof(BackoffPaths))]
	public void ReferenceEveryParameterItBinds(string path, bool usesNextAttemptAt, int? floorSeconds)
	{
		var request = Build(usesNextAttemptAt ? DateTimeOffset.UtcNow.AddMinutes(5) : null, floorSeconds);

		var referenced = SqlParameterTokens.ReferencedBy(request.Command.CommandText);
		var bound = SqlParameterTokens.BoundBy(request.Parameters);

		var orphaned = bound.Except(referenced, StringComparer.OrdinalIgnoreCase).ToList();
		orphaned.ShouldBeEmpty(
			$"the {path} path binds {SqlParameterTokens.Format(orphaned)} which its SQL never names. "
			+ "A predicate was removed and its binding was not — the value is now inert and misleading");
	}
}
