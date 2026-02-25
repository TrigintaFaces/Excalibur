// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Tests that <see cref="SqlServerLeaderElection"/> uses volatile for lock-free IsLeader reads (AD-540.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerLeaderElectionVolatileShould : UnitTestBase
{
	[Fact]
	public void DeclareIsLeaderFieldAsVolatile()
	{
		// Arrange
		var fieldInfo = typeof(SqlServerLeaderElection)
			.GetField("_isLeader", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert — the field must exist and be volatile (RequiredCustomModifiers contains IsVolatile)
		fieldInfo.ShouldNotBeNull();
		fieldInfo.FieldType.ShouldBe(typeof(bool));

		var requiredModifiers = fieldInfo.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(System.Runtime.CompilerServices.IsVolatile));
	}

	[Fact]
	public void ReturnFalseForIsLeaderByDefault()
	{
		// Arrange
		var leaderElection = CreateSqlServerLeaderElection();

		// Act & Assert — not started, should be false
		leaderElection.IsLeader.ShouldBeFalse();
	}

	[Fact]
	public void StillRetainLockObject()
	{
		// Arrange — verify _lock field still exists (needed for CurrentLeaderId + state transitions)
		var lockField = typeof(SqlServerLeaderElection)
			.GetField("_lock", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		lockField.ShouldNotBeNull("_lock must be retained for CurrentLeaderId and state transitions");
	}

	[Fact]
	public void ReadIsLeaderWithoutContention()
	{
		// Arrange
		var leaderElection = CreateSqlServerLeaderElection();

		// Act — concurrent reads should not deadlock or throw
		var results = new bool[1000];
		Parallel.For(0, 1000, i => { results[i] = leaderElection.IsLeader; });

		// Assert — all reads should return consistent value (false since not started)
		results.ShouldAllBe(v => v == false);
	}

	private static SqlServerLeaderElection CreateSqlServerLeaderElection()
	{
		return new SqlServerLeaderElection(
			"Server=localhost;Database=Test;Trusted_Connection=True;TrustServerCertificate=True;",
			"test-lock",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<SqlServerLeaderElection>.Instance);
	}
}
