// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for custom message validation.
/// </summary>
public interface IValidationService
{
	/// <summary>
	/// Validates a message using custom validation logic.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The validation context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Validation result indicating success or failure with errors. </returns>
	Task<MessageValidationResult> ValidateAsync(
		IDispatchMessage message,
		MessageValidationContext context,
		CancellationToken cancellationToken);
}
