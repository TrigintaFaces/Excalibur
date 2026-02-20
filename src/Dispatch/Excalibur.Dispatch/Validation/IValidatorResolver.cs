// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Provides message validation resolution services for the dispatch system. Resolves and executes appropriate validators based on message
/// type and content.
/// </summary>
public interface IValidatorResolver
{
	/// <summary>
	/// Attempts to validate the specified message using registered validators. Returns validation results if validators are available for
	/// the message type, or null if no validation is required.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns>
	/// A <see cref="IValidationResult" /> containing validation outcomes if validators exist; otherwise, null if no validation is needed
	/// for the message type.
	/// </returns>
	IValidationResult? TryValidate(IDispatchMessage message);
}
