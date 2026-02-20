// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Contract for custom input validators.
/// </summary>
public interface IInputValidator
{
	/// <summary>
	/// Validates a message and its context for custom business rules.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The message context to validate. </param>
	/// <returns> A task representing the validation result. </returns>
	Task<InputValidationResult> ValidateAsync(IDispatchMessage message, IMessageContext context);
}
