// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Base class for wait strategies.
/// </summary>
public abstract class WaitStrategyBase : IWaitStrategy
{
	private volatile bool _disposed;

	/// <inheritdoc />
	public abstract ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken);

	/// <summary>
	/// Resets the wait strategy state.
	/// </summary>
	public virtual void Reset()
	{
		// Base implementation - derived classes can override if they have state to reset
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the wait strategy.
	/// </summary>
	/// <param name="disposing"> Whether to dispose managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				// Dispose managed resources
			}

			_disposed = true;
		}
	}
}
