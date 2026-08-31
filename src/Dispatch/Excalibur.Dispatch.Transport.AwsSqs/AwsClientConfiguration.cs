// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Applies the configured connection settings onto an AWS SDK client configuration.
/// </summary>
/// <remarks>
/// The SDK's own <see cref="ClientConfig"/> is the endpoint/retry/timeout surface, so the connection
/// options are mapped onto it rather than reimplemented. Without this mapping a host that points the
/// transport at an alternate endpoint (a local emulator, a VPC endpoint) would silently reach the real
/// AWS service instead.
/// </remarks>
internal static class AwsClientConfiguration
{
	/// <summary>
	/// Applies region, endpoint override and transport-level settings from the provider options.
	/// </summary>
	/// <param name="config">The SDK client configuration to populate.</param>
	/// <param name="options">The configured provider options.</param>
	public static void Apply(ClientConfig config, AwsProviderOptions options)
	{
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(options);

		config.MaxErrorRetry = options.MaxRetryAttempts;
		config.Timeout = options.RequestTimeout;

		var serviceUrl = ResolveServiceUrl(options.Connection);
		if (serviceUrl is not null)
		{
			// ServiceURL and RegionEndpoint are mutually exclusive in the SDK: setting the region after
			// an explicit endpoint clears the endpoint, which is how an emulator host silently reaches
			// real AWS. The endpoint wins when one was configured.
			config.ServiceURL = serviceUrl.ToString();
			return;
		}

		if (!string.IsNullOrWhiteSpace(options.Region))
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
		}
	}

	/// <summary>
	/// Resolves the endpoint override, if any: an explicit service URL wins over the LocalStack URL.
	/// </summary>
	/// <param name="connection">The connection options.</param>
	/// <returns>The endpoint to target, or <see langword="null"/> to use the regional endpoint.</returns>
	public static Uri? ResolveServiceUrl(AwsSqsConnectionOptions connection)
	{
		ArgumentNullException.ThrowIfNull(connection);

		return connection.ServiceUrl ?? (connection.UseLocalStack ? connection.LocalStackUrl : null);
	}
}
