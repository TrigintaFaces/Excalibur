// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>
/// Boot-time validation that the configured <see cref="IKeyManagementProvider" /> keeps key material
/// across restarts, unless the host has explicitly accepted a volatile one.
/// </summary>
/// <remarks>
/// This validates the <em>provider's durability</em> rather than the shape of an options object, and it
/// runs at startup rather than on first use. First use is the wrong moment: by then the host is encrypting
/// real data under keys that will not exist tomorrow, and every one of those calls has already returned
/// success.
/// </remarks>
internal sealed class KeyDurabilityValidator : IValidateOptions<KeyDurabilityOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyDurabilityValidator" /> class.
	/// </summary>
	/// <param name="services"> The provider used to inspect the configured key-provider registration. </param>
	public KeyDurabilityValidator(IServiceProvider services) => _services = services;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KeyDurabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.AllowVolatileKeyProvider)
		{
			return ValidateOptionsResult.Success;
		}

		// Ask the registered provider itself whether it is durable. A durable provider answers for
		// IDurableKeyProvider through IServiceProvider.GetService; a volatile one answers null. This
		// resolves the provider that actually won registration (through any decorator, which forwards the
		// query), so registration order and wrapping do not matter.
		var provider = _services.GetService<IKeyManagementProvider>();
		if (provider?.GetService(typeof(IDurableKeyProvider)) is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Encryption is configured with a volatile in-memory key management provider. Key material would " +
			"be lost when the process exits, permanently and unrecoverably orphaning every value encrypted " +
			"under it, while each encrypt call still reported success. Register a durable provider (for " +
			"example the AWS KMS, Azure Key Vault, or HashiCorp Vault provider), or, for development and test " +
			"hosts only, accept the volatile provider explicitly by setting " +
			$"{nameof(KeyDurabilityOptions)}.{nameof(KeyDurabilityOptions.AllowVolatileKeyProvider)} to true.");
	}
}
