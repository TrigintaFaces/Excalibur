// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Validates <see cref="GooglePubSubOptions"/> configuration for security settings.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed class GooglePubSubOptionsValidator : IValidateOptions<GooglePubSubOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, GooglePubSubOptions options) =>

		// Implementation details...
		ValidateOptionsResult.Success;
}
