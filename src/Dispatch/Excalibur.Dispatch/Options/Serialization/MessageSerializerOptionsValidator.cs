// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Serialization;

/// <summary>
/// Validates <see cref="MessageSerializerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class MessageSerializerOptionsValidator : IValidateOptions<MessageSerializerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MessageSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.SerializerMap.Count == 0)
		{
			return ValidateOptionsResult.Fail($"{nameof(MessageSerializerOptions.SerializerMap)} must contain at least one serializer entry.");
		}

		return ValidateOptionsResult.Success;
	}
}
