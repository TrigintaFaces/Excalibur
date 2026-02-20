// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Timing;

/// <summary>
/// Implementation of timeout operation token for tracking operation performance. R7.4: High-performance timeout operation tracking.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimeoutOperationToken" /> class. </remarks>
/// <param name="operationType"> The operation type. </param>
/// <param name="context"> The timeout context. </param>
internal sealed class TimeoutOperationToken(TimeoutOperationType operationType, TimeoutContext? context = null) : ITimeoutOperationToken
{
	private readonly ValueStopwatch _stopwatch = ValueStopwatch.StartNew();
	private volatile bool _disposed;

	/// <inheritdoc />
	public Guid OperationId { get; } = Guid.NewGuid();

	/// <inheritdoc />
	public TimeoutOperationType OperationType { get; } = operationType;

	/// <inheritdoc />
	public TimeoutContext? Context { get; } = context;

	/// <inheritdoc />
	public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public TimeSpan Elapsed => _stopwatch.Elapsed;

	/// <inheritdoc />
	public bool IsCompleted { get; private set; }

	/// <inheritdoc />
	public bool? IsSuccessful { get; private set; }

	/// <inheritdoc />
	public bool? HasTimedOut { get; private set; }

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// Mark as completed if not already done
		if (!IsCompleted)
		{
			Complete(success: false, timedOut: false);
		}

		_disposed = true;
	}

	/// <summary>
	/// Marks the operation as completed with the specified result.
	/// </summary>
	/// <param name="success"> Whether the operation was successful. </param>
	/// <param name="timedOut"> Whether the operation timed out. </param>
	internal void Complete(bool success, bool timedOut)
	{
		if (_disposed)
		{
			return;
		}

		IsCompleted = true;
		IsSuccessful = success;
		HasTimedOut = timedOut;
	}
}
