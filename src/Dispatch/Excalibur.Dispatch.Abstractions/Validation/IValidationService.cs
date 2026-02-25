// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Provides validation services for message processing.
/// </summary>
public interface IValidationService
{
	/// <summary>
	/// Validates a message asynchronously.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The validation context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous validation operation. </returns>
	ValueTask<ValidationResult> ValidateAsync(object message, MessageValidationContext context,
		CancellationToken cancellationToken);
}
