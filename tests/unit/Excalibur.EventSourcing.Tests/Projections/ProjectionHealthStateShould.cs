// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Unit tests for ProjectionHealthState -- thread-safe health state tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHealthStateShould
{
	private readonly ProjectionHealthState _state = new();

	[Fact]
	public void DefaultToNoError()
	{
		_state.LastInlineError.ShouldBeNull();
		_state.LastErrorProjectionType.ShouldBeNull();
	}

	[Fact]
	public void RecordInlineErrorWithTimestamp()
	{
		// Act
		_state.RecordInlineError("OrderSummary");

		// Assert
		_state.LastInlineError.ShouldNotBeNull();
		_state.LastInlineError.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5));
		_state.LastErrorProjectionType.ShouldBe("OrderSummary");
	}

	[Fact]
	public void OverwritePreviousErrorOnNewError()
	{
		// Arrange
		_state.RecordInlineError("OrderSummary");
		var firstError = _state.LastInlineError;

		// Act
		_state.RecordInlineError("InventoryView");

		// Assert -- updated
		_state.LastErrorProjectionType.ShouldBe("InventoryView");
		_state.LastInlineError.ShouldNotBeNull();
	}

	[Fact]
	public void HandleConcurrentErrorRecording()
	{
		// Act -- concurrent error recording
		Parallel.For(0, 50, i =>
		{
			_state.RecordInlineError($"Projection-{i}");
		});

		// Assert -- last error is one of the recorded ones (no corruption)
		_state.LastInlineError.ShouldNotBeNull();
		_state.LastErrorProjectionType.ShouldNotBeNull();
		_state.LastErrorProjectionType.ShouldStartWith("Projection-");
	}

	[Fact]
	public void ReportNoAppliedPosition_BeforeAnythingIsApplied()
	{
		// Null means "this process has applied nothing for that subscription", which a consistent read
		// must be able to tell apart from "applied through position 0".
		_state.AppliedPosition("orders").ShouldBeNull();
	}

	[Fact]
	public void ReportTheAppliedPosition_PerSubscription()
	{
		_state.RecordApplied("orders", 42);
		_state.RecordApplied("invoices", 7);

		_state.AppliedPosition("orders").ShouldBe(42);
		_state.AppliedPosition("invoices").ShouldBe(7, "subscriptions advance independently");
	}

	[Fact]
	public void NeverLetTheAppliedMarkMoveBackwards()
	{
		// A reader that resumes from a winner's checkpoint legitimately re-reads a LOWER position. If the
		// published mark followed it down, a consistent read that had already been satisfied would start
		// failing -- the wait would un-succeed.
		_state.RecordApplied("orders", 100);
		_state.RecordApplied("orders", 40);

		_state.AppliedPosition("orders").ShouldBe(100);
	}

	[Fact]
	public void RaiseTheAppliedMarkUnderConcurrentAdvances()
	{
		// The mark is written from a polling loop and read from request threads, so the raise must be
		// atomic rather than read-then-write.
		Parallel.For(0, 500, i => _state.RecordApplied("orders", i));

		_state.AppliedPosition("orders").ShouldBe(499);
	}
}
