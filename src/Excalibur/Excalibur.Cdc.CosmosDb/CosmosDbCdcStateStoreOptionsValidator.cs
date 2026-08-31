// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Validates <see cref="CosmosDbCdcStateStoreOptions"/> using the <see cref="IValidateOptions{TOptions}"/> pattern.
/// </summary>
public sealed class CosmosDbCdcStateStoreOptionsValidator : IValidateOptions<CosmosDbCdcStateStoreOptions>
{
	// The client the store will actually use, resolved from the same container by the same lookup the store's
	// registration performs. Holding the client itself rather than a flag is what stops the two answers
	// diverging: there is no separately-settable "a client was supplied" marker to get out of step with
	// whether one was.
	private readonly CosmosClient? _suppliedClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcStateStoreOptionsValidator"/> class for a store
	/// that builds its own client, so <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/> is required.
	/// </summary>
	public CosmosDbCdcStateStoreOptionsValidator()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcStateStoreOptionsValidator"/> class.
	/// </summary>
	/// <param name="suppliedClient">
	/// The client registered in the container, or <see langword="null"/> when none is. When one is present the
	/// store uses it and never reads the connection string, so requiring one would reject a host that
	/// authenticates by token credential — the configuration this overload exists to permit.
	/// </param>
	internal CosmosDbCdcStateStoreOptionsValidator(CosmosClient? suppliedClient) => _suppliedClient = suppliedClient;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CosmosDbCdcStateStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("CosmosDb CDC state store options cannot be null.");
		}

		if (_suppliedClient is null && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"ConnectionString is required unless a CosmosClient is registered for the store to use.");
		}

		if (string.IsNullOrWhiteSpace(options.DatabaseId))
		{
			return ValidateOptionsResult.Fail("DatabaseId is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ContainerId))
		{
			return ValidateOptionsResult.Fail("ContainerId is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
