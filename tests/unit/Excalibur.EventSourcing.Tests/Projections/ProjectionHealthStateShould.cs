// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	public void DefaultToZeroLag()
	{
		_state.AsyncLag.ShouldBe(0);
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
	public void TrackAsyncLag()
	{
		// Act
		_state.AsyncLag = 500;

		// Assert
		_state.AsyncLag.ShouldBe(500);
	}

	[Fact]
	public void HandleConcurrentAsyncLagUpdates()
	{
		// Act -- concurrent writers
		Parallel.For(0, 100, i =>
		{
			_state.AsyncLag = i;
		});

		// Assert -- value is one of the written values (no corruption)
		_state.AsyncLag.ShouldBeInRange(0, 99);
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
}
