// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="MarkMessageFailedRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MarkMessageFailedRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";
	private const string TestMessageId = "msg-12345";
	private const string TestErrorMessage = "Connection timeout";
	private const string TestLeasedBy = "processor-1";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(null!, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest("", TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest("   ", TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, null!, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, "", TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, "   ", TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullErrorMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MarkMessageFailedRequest(TestTableName, TestMessageId, null!, 1, TestLeasedBy, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullLeasedBy()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceLeasedBy()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, "   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestTableName);
		request.Command.CommandText.ShouldContain("Status = 3");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCommandThatSetsLastError()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = @ErrorMessage");
	}

	[Fact]
	public void CreateCommandThatSetsRetryCountNonDecreasing()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 3, TestLeasedBy, 30, CancellationToken.None);

		// Assert — RetryCount is applied non-decreasing (a stale late writer must not lower the persisted
		// count and weaken the DLQ-ceiling termination guarantee), so the SET clause is a max, not a plain assign.
		request.Command.CommandText.ShouldContain("RetryCount = CASE WHEN RetryCount > @RetryCount THEN RetryCount ELSE @RetryCount END");
	}

	[Fact]
	public void CreateCommandThatSetsLastAttemptAt()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastAttemptAt = @LastAttemptAt");
	}

	[Fact]
	public void CreateCommandThatReleasesTheLease()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert — the failed transition must clear the lease (parity with the dead-letter/sent transitions),
		// so the computed backoff governs the next claim and statistics report the row as failed, not in-flight.
		request.Command.CommandText.ShouldContain("LeasedAt = NULL");
		request.Command.CommandText.ShouldContain("LeasedBy = NULL");
	}

	[Fact]
	public void CreateCommandThatGuardsOnLeaseOwnership()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert — a stale processor must not mark-failed a row a peer has re-claimed: the update only affects
		// the row when it is unleased or still leased by this processor.
		request.Command.CommandText.ShouldContain("LeasedBy IS NULL OR LeasedBy = @LeasedBy");
	}

	#endregion

	#region Retry Count Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void AcceptValidRetryCount(int retryCount)
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, retryCount, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region Empty Error Message Tests

	[Fact]
	public void AcceptEmptyErrorMessage()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, "", 1, TestLeasedBy, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
