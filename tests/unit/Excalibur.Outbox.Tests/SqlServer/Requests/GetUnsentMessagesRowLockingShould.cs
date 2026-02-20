// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Tests for UPDLOCK/READPAST row locking hints in <see cref="GetUnsentMessagesRequest"/> (AD-540.6).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GetUnsentMessagesRowLockingShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	[Fact]
	public void IncludeUpdlockHint()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("UPDLOCK");
	}

	[Fact]
	public void IncludeReadpastHint()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("READPAST");
	}

	[Fact]
	public void IncludeWithClauseForRowLocking()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert — both hints must be in the same WITH clause
		request.Command.CommandText.ShouldContain("WITH (UPDLOCK, READPAST)");
	}

	[Fact]
	public void IncludeOrderByForDeterministicBatchSelection()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert — deterministic ordering is required for competing consumers
		request.Command.CommandText.ShouldContain("ORDER BY");
		request.Command.CommandText.ShouldContain("CreatedAt ASC");
	}
}
