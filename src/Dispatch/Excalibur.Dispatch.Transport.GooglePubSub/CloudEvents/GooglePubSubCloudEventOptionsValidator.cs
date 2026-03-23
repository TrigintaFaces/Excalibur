// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.GooglePubSub;

/// <summary>
/// Validates <see cref="GooglePubSubCloudEventOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class GooglePubSubCloudEventOptionsValidator : IValidateOptions<GooglePubSubCloudEventOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GooglePubSubCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Google Pub/Sub CloudEvent options cannot be null.");
		}

		if (options.MaxMessageSizeBytes <= 0)
		{
			return ValidateOptionsResult.Fail(
				"GooglePubSubCloudEventOptions.MaxMessageSizeBytes must be greater than zero.");
		}

		if (options.AckDeadline <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				"GooglePubSubCloudEventOptions.AckDeadline must be greater than zero.");
		}

		if (options.Transport is not null && options.Transport.CompressionThreshold <= 0)
		{
			return ValidateOptionsResult.Fail(
				"CloudEventTransportOptions.CompressionThreshold must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
