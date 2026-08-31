// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Tests.Shared.Helpers;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Tests the instrument the parameter-binding locks depend on.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SqlParameterTokens"/> is not production code, which is exactly why it needs arms: every other
/// lock in this directory reports what it says, so a scanner that quietly stopped seeing a token would turn
/// those locks green without anyone touching them. That is the failure this whole bead is about, one layer
/// down.
/// </para>
/// <para>
/// The <c>DECLARE</c> exclusion is the part most likely to be widened under pressure. It exists because
/// T-SQL locals wear the same sigil as parameters, and one shipped batch declares four of them; without the
/// exclusion that statement reports four phantom unbound parameters and the obvious fix is to delete the
/// arm. The arms below pin the exclusion to the narrow thing it is for: a local is excluded, and a real
/// parameter standing next to one is not.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlParameterTokensShould : UnitTestBase
{
	/// <summary>LIVENESS. Ordinary bound parameters are seen.</summary>
	[Fact]
	public void ReportEveryParameterAStatementReferences() =>
		SqlParameterTokens.ReferencedBy("UPDATE t SET a = @Value WHERE Id = @MessageId")
			.ShouldBe(["Value", "MessageId"], ignoreOrder: true);

	/// <summary>
	/// SAFETY. A local the batch declares for itself is not reported as a parameter the caller must bind.
	/// </summary>
	[Fact]
	public void NotReportALocalTheBatchDeclaresForItself() =>
		SqlParameterTokens.ReferencedBy("DECLARE @AllSent BIT;\nUPDATE t SET Status = @AllSent WHERE Id = @MessageId")
			.ShouldBe(["MessageId"], ignoreOrder: true);

	/// <summary>
	/// LIVENESS, and the arm that stops the exclusion being widened into uselessness: a real parameter used
	/// in the same batch as a declared local is still reported. An exclusion that swallowed the rest of the
	/// statement would drop it, and every downstream lock would go green while seeing nothing.
	/// </summary>
	[Fact]
	public void StillReportRealParametersInABatchThatDeclaresLocals()
	{
		var sql = """
			DECLARE @A BIT, @B BIT;
			SELECT @A = 1, @B = 0 FROM t WHERE Id = @MessageId;
			UPDATE t SET Status = CASE WHEN @A = 1 THEN 2 ELSE 3 END WHERE Id = @MessageId AND Owner = @LeasedBy;
			""";

		SqlParameterTokens.ReferencedBy(sql).ShouldBe(["MessageId", "LeasedBy"], ignoreOrder: true);
	}

	/// <summary>
	/// SAFETY. An engine variable is not a parameter, and must not be mistaken for one — a phantom unbound
	/// parameter is the pressure that gets a lock deleted.
	/// </summary>
	[Fact]
	public void NotMistakeAnEngineVariableForAParameter() =>
		SqlParameterTokens.ReferencedBy("UPDATE t SET a = 1 WHERE Id = @MessageId; SELECT @@ROWCOUNT")
			.ShouldBe(["MessageId"], ignoreOrder: true);

	/// <summary>LIVENESS. Bound names are read back without their sigil, so the two sets are comparable.</summary>
	[Fact]
	public void ReportBoundParametersWithoutTheirSigil()
	{
		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", "m");
		parameters.Add("@LeasedBy", "p");

		SqlParameterTokens.BoundBy(parameters).ShouldBe(["MessageId", "LeasedBy"], ignoreOrder: true);
	}
}
