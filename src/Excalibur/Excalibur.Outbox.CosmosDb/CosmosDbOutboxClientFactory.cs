// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Builds the Cosmos client the outbox store talks through.
/// </summary>
/// <remarks>
/// Kept apart from the store so the store owns the outbox protocol — queries, conditional writes,
/// telemetry — and this owns connection setup. They change for different reasons, and connection setup
/// pulls in a cluster of SDK and serialization types the store otherwise has no use for.
/// </remarks>
internal static class CosmosDbOutboxClientFactory
{
	/// <summary>
	/// Creates a client from the supplied outbox options.
	/// </summary>
	/// <param name="options">The outbox options carrying the connection and client settings.</param>
	/// <returns>A client bound to the configured account.</returns>
	public static CosmosClient Create(CosmosDbOutboxOptions options)
	{
		var clientOptions = CreateClientOptions(options);

		return !string.IsNullOrWhiteSpace(options.Connection.ConnectionString)
			? new CosmosClient(options.Connection.ConnectionString, clientOptions)
			: new CosmosClient(options.Connection.AccountEndpoint, options.Connection.AccountKey, clientOptions);
	}

	private static CosmosClientOptions CreateClientOptions(CosmosDbOutboxOptions options)
	{
		var clientOptions = new CosmosClientOptions
		{
			ApplicationName = "Excalibur.Outbox.CosmosDb",
			MaxRetryAttemptsOnRateLimitedRequests = options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(options.MaxRetryWaitTimeInSeconds),
			ConnectionMode = options.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,

			// The framework-built client uses System.Text.Json so that persisted documents'
			// [JsonPropertyName] attributes are honored — the SDK's default Newtonsoft serializer ignores
			// them, which would silently change every property name on the wire.
			UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			},
		};

		// Supplied only where the caller must control the transport — chiefly the emulator, which a
		// default client cannot reach.
		if (options.HttpClientFactory is not null)
		{
			clientOptions.HttpClientFactory = options.HttpClientFactory;
		}

		return clientOptions;
	}
}
