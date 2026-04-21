// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Validates <see cref="PluggableSerializationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class PluggableSerializationOptionsValidator : IValidateOptions<PluggableSerializationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PluggableSerializationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// PluggableSerializationOptions is valid with no registrations (consumers may register later).
		// CurrentSerializerName is optional (null means no default).
		return ValidateOptionsResult.Success;
	}
}
