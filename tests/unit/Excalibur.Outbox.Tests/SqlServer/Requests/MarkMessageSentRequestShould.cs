// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="MarkMessageSentRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkMessageSentRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";
	private const string TestMessageId = "msg-12345";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest(null!, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest("", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest("   ", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest(TestTableName, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest(TestTableName, "", 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkMessageSentRequest(TestTableName, "   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestTableName);
		request.Command.CommandText.ShouldContain("Status = 2");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCommandThatClearsLastError()
	{
		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = NULL");
	}

	[Fact]
	public void CreateCommandThatSetsSentAt()
	{
		// Act
		var request = new MarkMessageSentRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("SentAt = @SentAt");
	}

	#endregion
}
