// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.GooglePubSub;

/// <summary>
/// Validates <see cref="GooglePubSubOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class GooglePubSubOptionsValidator : IValidateOptions<GooglePubSubOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GooglePubSubOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Google Pub/Sub options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.ProjectId))
		{
			return ValidateOptionsResult.Fail(
				"Google Pub/Sub ProjectId is required. Set GooglePubSubOptions.ProjectId to your Google Cloud project ID.");
		}

		if (string.IsNullOrWhiteSpace(options.SubscriptionId))
		{
			return ValidateOptionsResult.Fail(
				"Google Pub/Sub SubscriptionId is required. Set GooglePubSubOptions.SubscriptionId to the target subscription.");
		}

		return ValidateOptionsResult.Success;
	}
}
