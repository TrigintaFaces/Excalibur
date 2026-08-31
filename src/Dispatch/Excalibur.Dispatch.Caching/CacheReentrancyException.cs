// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Thrown when a handler whose result is cached dispatches another message that resolves to the
/// cache key currently being produced, directly or through a further dispatch.
/// </summary>
/// <remarks>
/// <para>
/// The inner dispatch waits for the outer one to publish its value, and the outer cannot publish
/// until the inner returns, so the request would never complete. The cycle is a fault in the
/// handler's own composition, not a fault of the cache backend, and it is reported rather than
/// absorbed: it is deliberately excluded from the fail-open fallback that covers backend errors,
/// because falling back would run the handler a second time and return success while leaving the
/// cycle in place and invisible.
/// </para>
/// <para>
/// Resolve it by giving the two messages distinct cache keys, or by moving the nested dispatch
/// outside the cached handler.
/// </para>
/// <para>
/// Derives from <see cref="InvalidOperationException"/> because the condition is a misuse of the
/// dispatch pipeline rather than a transient failure; retrying without changing the handler or the
/// keys produces the same result.
/// </para>
/// </remarks>
public sealed class CacheReentrancyException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CacheReentrancyException"/> class.
	/// </summary>
	public CacheReentrancyException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheReentrancyException"/> class with a
	/// specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public CacheReentrancyException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheReentrancyException"/> class with a
	/// specified error message and a reference to the inner exception that caused it.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused the current exception.</param>
	public CacheReentrancyException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheReentrancyException"/> class for a
	/// specific cache key.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="cacheKey">The resolved cache key that was re-entered.</param>
	public CacheReentrancyException(string message, string? cacheKey)
		: base(message) => CacheKey = cacheKey;

	/// <summary>
	/// Gets the resolved cache key that was re-entered while it was being produced.
	/// </summary>
	/// <value>The cache key, or <see langword="null"/> when the key was not supplied.</value>
	public string? CacheKey { get; }
}
