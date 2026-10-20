// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.InMemory;

/// <summary>Validates the serialization path available to the in-memory event store.</summary>
internal sealed class InMemoryEventStoreOptionsValidator : IValidateOptions<InMemoryEventStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, InMemoryEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		// Store names are DI keys; the store consumes only IOptions<T>.Value's default profile.
		if (name is not null && name != Options.DefaultName)
		{
			return ValidateOptionsResult.Skip;
		}

		return JsonSerializer.IsReflectionEnabledByDefault || options.EventTypeInfoResolver is not null
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(
				$"{nameof(InMemoryEventStoreOptions.EventTypeInfoResolver)} is required when JSON reflection is disabled.");
	}
}
