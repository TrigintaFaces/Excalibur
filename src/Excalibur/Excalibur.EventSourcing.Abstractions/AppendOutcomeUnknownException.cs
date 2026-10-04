// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>A save could not establish whether its atomic append committed.</summary>
/// <remarks>
/// Preserve pending events and their original identities, payloads and expected version for
/// reconciliation. Do not reload and regenerate the operation on the assumption that nothing was
/// written. A transactional append may have committed both events and outbox rows.
/// </remarks>
public sealed class AppendOutcomeUnknownException : InvalidOperationException
{
	/// <summary>Initializes an exception with a reconciliation instruction.</summary>
	public AppendOutcomeUnknownException()
		: base("The append outcome is unknown. Preserve the original operation and reconcile before retrying or publishing effects.")
	{
	}

	/// <summary>Initializes an exception with the supplied message.</summary>
	/// <param name="message">The diagnostic message.</param>
	public AppendOutcomeUnknownException(string? message) : base(message)
	{
	}

	/// <summary>Initializes an exception with the supplied message and cause.</summary>
	/// <param name="message">The diagnostic message.</param>
	/// <param name="innerException">The original cause.</param>
	public AppendOutcomeUnknownException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}
