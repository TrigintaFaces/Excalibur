// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for custom input sanitization logic.
/// </summary>
public interface ISanitizationService
{
	/// <summary>
	/// Sanitizes a value based on custom rules.
	/// </summary>
	/// <param name="value"> The value to sanitize. </param>
	/// <param name="propertyName"> The name of the property being sanitized. </param>
	/// <param name="messageType"> The type of the message containing the property. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The sanitized value. </returns>
	Task<object?> SanitizeValueAsync(
		object? value,
		string propertyName,
		Type messageType,
		CancellationToken cancellationToken);
}
