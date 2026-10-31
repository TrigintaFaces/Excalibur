// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// <param name="requestServices">The caller-owned request-scoped provider, when available.</param>
	/// <param name="cancellationToken">Cancellation of validation.</param>
	/// <remarks>
	/// Borrowed request services take precedence over ambient services and are never disposed by the resolver.
	/// A resolver requiring services creates and asynchronously disposes its own scope when neither is available.
	/// Validation failures are returned; cancellation and infrastructure exceptions propagate.
	/// </remarks>
	/// <returns>
	/// A <see cref="IValidationResult" /> containing validation outcomes if validators exist; otherwise, null if no validation is needed
	/// for the message type.
	/// </returns>
	ValueTask<IValidationResult?> TryValidateAsync(
		IDispatchMessage message, IServiceProvider? requestServices = null, CancellationToken cancellationToken = default);
}
