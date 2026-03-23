// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Tests for atomic claim and concurrency safety in <see cref="GetUnsentMessagesRequest"/>.
/// The implementation uses UPDATE...OUTPUT with lease columns for atomic claim+fetch,
/// replacing the previous UPDLOCK/READPAST SELECT pattern (S690 T.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GetUnsentMessagesRowLockingShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	[Fact]
	public void UseUpdateTopForAtomicClaim()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, 300, "test-processor", CancellationToken.None);

		// Assert - UPDATE TOP atomically claims rows
		request.Command.CommandText.ShouldContain("UPDATE TOP");
	}

	[Fact]
	public void IncludeOutputClauseForClaimedRows()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, 300, "test-processor", CancellationToken.None);

		// Assert - OUTPUT returns the claimed rows in a single atomic operation
		request.Command.CommandText.ShouldContain("OUTPUT");
	}

	[Fact]
	public void SetLeaseOwnershipOnClaim()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, 300, "test-processor", CancellationToken.None);

		// Assert - Lease columns prevent double-processing by concurrent processors
		request.Command.CommandText.ShouldContain("LeasedAt = GETUTCDATE()");
		request.Command.CommandText.ShouldContain("LeasedBy = @ProcessorId");
	}

	[Fact]
	public void ReclaimStaleLeases()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, 300, "test-processor", CancellationToken.None);

		// Assert - Stale leases older than timeout are automatically reclaimed
		request.Command.CommandText.ShouldContain("LeasedAt IS NULL");
		request.Command.CommandText.ShouldContain("@LeaseTimeoutSeconds");
	}

	[Fact]
	public void IncludeStatusFilterForDeterministicBatchSelection()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, 300, "test-processor", CancellationToken.None);

		// Assert - Only processes Staged (0), Failed (3), PartiallyFailed (4)
		request.Command.CommandText.ShouldContain("Status IN (0, 3, 4)");
	}
}
