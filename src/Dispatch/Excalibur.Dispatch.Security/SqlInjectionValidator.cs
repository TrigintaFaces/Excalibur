// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Validator that checks for potential SQL injection patterns in message content.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed class SqlInjectionValidator : IInputValidator
{
	/// <inheritdoc/>
	public Task<InputValidationResult> ValidateAsync(IDispatchMessage message, IMessageContext context) =>

		// Stub implementation - returns success until validation logic is implemented
		Task.FromResult(InputValidationResult.Success());
}
