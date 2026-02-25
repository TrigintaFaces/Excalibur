// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Interface for custom validators in a profile.
/// </summary>
public interface ICustomValidator
{
	/// <summary>
	/// Validates a message using custom validation logic.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The validation result. </returns>
	IValidationResult Validate(IDispatchMessage message, IMessageContext context);
}
