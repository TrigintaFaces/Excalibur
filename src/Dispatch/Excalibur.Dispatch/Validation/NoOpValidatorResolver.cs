// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// A no-operation implementation of <see cref="IValidatorResolver" /> that performs no validation. Used as a default implementation when
/// message validation is disabled or not required, providing a null object pattern to avoid conditional checks throughout the system.
/// </summary>
public sealed class NoOpValidatorResolver : IValidatorResolver
{
	/// <summary>
	/// Always returns null, indicating no validation is performed on any message. This implementation bypasses all validation logic for
	/// improved performance when validation is not needed in the application.
	/// </summary>
	/// <param name="message"> The message to validate (ignored in this implementation). </param>
	/// <returns> Always returns null, indicating no validation was performed. </returns>
	public IValidationResult? TryValidate(IDispatchMessage message) => null;
}
