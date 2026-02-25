// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Transport.Google;

internal readonly record struct GooglePubSubDeliveryOptions(
		bool EnableExactlyOnceDelivery,
		bool EnableMessageOrdering)
{
	public static GooglePubSubDeliveryOptions Resolve(
			GoogleProviderOptions providerOptions,
			GooglePubSubCloudEventOptions? cloudEventOptions)
	{
		ArgumentNullException.ThrowIfNull(providerOptions);

		var enableExactlyOnce = providerOptions.EnableExactlyOnceDelivery;
		var enableOrdering = providerOptions.EnableMessageOrdering;

		if (cloudEventOptions is not null)
		{
			enableExactlyOnce |= cloudEventOptions.UseExactlyOnceDelivery;
			enableOrdering |= cloudEventOptions.UseOrderingKeys;
		}

		return new GooglePubSubDeliveryOptions(enableExactlyOnce, enableOrdering);
	}
}
