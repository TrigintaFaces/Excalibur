// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="MarkMessageFailedRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkMessageFailedRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";
	private const string TestMessageId = "msg-12345";
	private const string TestErrorMessage = "Connection timeout";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(null!, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest("", TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest("   ", TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, null!, TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, "", TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageFailedRequest(TestTableName, "   ", TestErrorMessage, 1, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullErrorMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MarkMessageFailedRequest(TestTableName, TestMessageId, null!, 1, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None);

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
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCommandThatSetsLastError()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = @ErrorMessage");
	}

	[Fact]
	public void CreateCommandThatSetsRetryCount()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 3, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("RetryCount = @RetryCount");
	}

	[Fact]
	public void CreateCommandThatSetsLastAttemptAt()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, 1, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastAttemptAt = @LastAttemptAt");
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
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, TestErrorMessage, retryCount, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region Empty Error Message Tests

	[Fact]
	public void AcceptEmptyErrorMessage()
	{
		// Act
		var request = new MarkMessageFailedRequest(TestTableName, TestMessageId, "", 1, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
