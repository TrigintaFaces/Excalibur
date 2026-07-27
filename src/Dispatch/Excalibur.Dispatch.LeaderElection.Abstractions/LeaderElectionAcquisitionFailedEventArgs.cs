// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Event arguments raised when a leader election candidate fails to acquire leadership,
/// either by losing the acquisition race to another candidate or because an error occurred
/// during the acquisition attempt.
/// </summary>
/// <param name="candidateId">The identifier of the candidate whose acquisition attempt failed.</param>
/// <param name="resourceName">The name of the resource the candidate was contending for.</param>
/// <param name="reason">A short, human-readable description of why the acquisition failed.</param>
/// <param name="timestamp">The time at which the failure occurred, supplied by the caller from its configured time source.</param>
/// <param name="exception">The exception that caused the failure, or <see langword="null"/> when the failure was a lost race rather than an error.</param>
public sealed class LeaderElectionAcquisitionFailedEventArgs(
	string candidateId,
	string resourceName,
	string reason,
	DateTimeOffset timestamp,
	Exception? exception = null) : EventArgs
{
	/// <summary>
	/// Gets the candidate ID whose acquisition attempt failed.
	/// </summary>
	/// <value>the candidate ID whose acquisition attempt failed.</value>
	public string CandidateId { get; } = candidateId;

	/// <summary>
	/// Gets the resource name the candidate was contending for.
	/// </summary>
	/// <value>the resource name the candidate was contending for.</value>
	public string ResourceName { get; } = resourceName;

	/// <summary>
	/// Gets the reason the acquisition failed.
	/// </summary>
	/// <value>a short, human-readable description of why the acquisition failed.</value>
	public string Reason { get; } = reason;

	/// <summary>
	/// Gets the exception that caused the failure, if any.
	/// </summary>
	/// <value>the exception that caused the failure, or <see langword="null"/> when the failure was a lost race rather than an error.</value>
	public Exception? Exception { get; } = exception;

	/// <summary>
	/// Gets when the failure occurred.
	/// </summary>
	/// <value>when the failure occurred.</value>
	public DateTimeOffset Timestamp { get; } = timestamp;
}
