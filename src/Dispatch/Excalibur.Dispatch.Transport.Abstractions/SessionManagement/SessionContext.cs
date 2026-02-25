// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a session context.
/// </summary>
public class SessionContext : IDisposable, IAsyncDisposable
{
	private volatile bool _disposed;

	/// <summary>
	/// Gets the session identifier.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string SessionId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the session information.
	/// </summary>
	/// <value>
	/// The session information.
	/// </value>
	public SessionInfo Info { get; init; } = new();

	/// <summary>
	/// Gets the session options.
	/// </summary>
	/// <value>
	/// The session options.
	/// </value>
	public SessionOptions Options { get; init; } = new();

	/// <summary>
	/// Gets or sets the session state data.
	/// </summary>
	/// <value>The current <see cref="StateData"/> value.</value>
	public object? StateData { get; set; }

	/// <summary>
	/// Gets or sets the completion callback.
	/// </summary>
	/// <value>The current <see cref="OnCompletion"/> value.</value>
	public Func<SessionContext, Task>? OnCompletion { get; set; }

	/// <summary>
	/// Disposes the session context.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the session context asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			if (OnCompletion != null)
			{
				await OnCompletion(this).ConfigureAwait(false);
			}

			_disposed = true;
		}

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the session context.
	/// </summary>
	/// <param name="disposing"> Whether to dispose managed resources. </param>
	/// <remarks>
	/// <para>
	/// If an async completion callback is registered, prefer <see cref="DisposeAsync"/> for deterministic
	/// completion. Synchronous dispose triggers completion without waiting.
	/// </para>
	/// </remarks>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_ = OnCompletion?.Invoke(this);
			}

			_disposed = true;
		}
	}
}
