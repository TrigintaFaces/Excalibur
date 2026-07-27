// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Thrown by <see cref="IFencedResource.GuardAsync(long, System.Threading.CancellationToken)"/> when the
/// presented fencing token is not strictly greater than the highest token the resource has already
/// observed — the Chubby/Lamport sequencer rule that rejects operations from a stale leader.
/// </summary>
public sealed class StaleFencingTokenException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="StaleFencingTokenException"/> class.
	/// </summary>
	public StaleFencingTokenException()
		: base("The presented fencing token is stale and was rejected.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StaleFencingTokenException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public StaleFencingTokenException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StaleFencingTokenException"/> class with a message
	/// and an inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public StaleFencingTokenException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the fencing token that was presented and rejected, when known.
	/// </summary>
	public long? PresentedToken { get; init; }

	/// <summary>
	/// Gets the highest fencing token previously observed by the guarded resource, when known.
	/// </summary>
	public long? HighWaterToken { get; init; }
}
