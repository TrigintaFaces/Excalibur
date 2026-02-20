// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="MemoryMessageEventArgs"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class MemoryMessageEventArgsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsEnvelopeProperty()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act
		var args = new MemoryMessageEventArgs(envelope, CancellationToken.None);

		// Assert
		args.Envelope.ShouldBe(envelope);
	}

	[Fact]
	public void Constructor_SetsCancellationTokenProperty()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		// Act
		var args = new MemoryMessageEventArgs(envelope, token);

		// Assert
		args.CancellationToken.ShouldBe(token);
	}

	[Fact]
	public void Constructor_WithNullEnvelope_SetsEnvelopeToNull()
	{
		// Act
		var args = new MemoryMessageEventArgs(null!, CancellationToken.None);

		// Assert
		args.Envelope.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithCancelledToken_SetsCancellationToken()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var cancelledToken = cts.Token;

		// Act
		var args = new MemoryMessageEventArgs(envelope, cancelledToken);

		// Assert
		args.CancellationToken.IsCancellationRequested.ShouldBeTrue();
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromEventArgs()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		var args = new MemoryMessageEventArgs(envelope, CancellationToken.None);

		// Assert
		_ = args.ShouldBeAssignableTo<EventArgs>();
	}

	#endregion

	#region CancellationToken Tests

	[Fact]
	public void CancellationToken_DefaultIsNone()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		var args = new MemoryMessageEventArgs(envelope, default);

		// Assert
		args.CancellationToken.ShouldBe(CancellationToken.None);
	}

	[Fact]
	public void CancellationToken_CanBeUsedForCancellation()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		using var cts = new CancellationTokenSource();
		var args = new MemoryMessageEventArgs(envelope, cts.Token);

		// Act
		cts.Cancel();

		// Assert
		args.CancellationToken.IsCancellationRequested.ShouldBeTrue();
	}

	#endregion
}
