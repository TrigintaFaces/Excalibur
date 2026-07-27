// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Boot-time validation that the configured <see cref="IGrantStore" /> keeps grants across restarts,
/// unless the host has explicitly accepted a volatile one.
/// </summary>
/// <remarks>
/// This validates the <em>store's durability</em> rather than the shape of an options object. The failure
/// it prevents is quiet in an unusual way: a lost grant set does not break authorization, it makes
/// authorization deny everyone, because a user whose grants vanished looks exactly like a user who never
/// had any.
/// </remarks>
internal sealed class GrantDurabilityValidator : IValidateOptions<GrantDurabilityOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrantDurabilityValidator" /> class.
	/// </summary>
	/// <param name="services"> The provider used to inspect the configured grant-store registration. </param>
	public GrantDurabilityValidator(IServiceProvider services) => _services = services;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GrantDurabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.AllowVolatileGrantStore)
		{
			return ValidateOptionsResult.Success;
		}

		// Ask the registered store itself whether it is durable. A durable store answers for
		// IDurableGrantStore through IServiceProvider.GetService; a volatile one answers null. This
		// resolves the store that actually won registration (through any decorator, which forwards the
		// query), so registration order and wrapping do not matter.
		var store = _services.GetService<IGrantStore>();
		if (store?.GetService(typeof(IDurableGrantStore)) is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Authorization is configured with a volatile in-memory grant store. Every grant would be lost " +
			"when the process exits, after each was reported as saved, and authorization would then deny " +
			"every user rather than fail visibly. Register a durable grant store (for example the Postgres, " +
			"Cosmos DB, DynamoDB, Firestore, or MongoDB grant store), or, for development and test hosts only, " +
			"accept the volatile store explicitly by setting " +
			$"{nameof(GrantDurabilityOptions)}.{nameof(GrantDurabilityOptions.AllowVolatileGrantStore)} to true.");
	}
}
