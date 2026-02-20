// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Interface for validating message context integrity.
/// </summary>
public interface IContextValidator
{
	/// <summary>
	/// Validates the message context for integrity and completeness.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context to validate. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A validation result containing details about any issues found. </returns>
	ValueTask<ContextValidationResult> ValidateAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);
}
