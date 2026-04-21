// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Validates <see cref="EventGridTransportOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class EventGridTransportOptionsValidator : IValidateOptions<EventGridTransportOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EventGridTransportOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Event Grid transport options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.TopicEndpoint))
		{
			return ValidateOptionsResult.Fail(
				"TopicEndpoint is required. Set EventGridTransportOptions.TopicEndpoint to the Event Grid topic endpoint URI.");
		}

		return ValidateOptionsResult.Success;
	}
}
