// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Validates <see cref="GrpcTransportOptions"/> for cross-property constraints
/// that cannot be expressed with DataAnnotations alone.
/// </summary>
internal sealed class GrpcTransportOptionsValidator : IValidateOptions<GrpcTransportOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GrpcTransportOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ServerAddress))
		{
			return ValidateOptionsResult.Fail("ServerAddress must not be empty. Provide a valid gRPC server URI (e.g., 'https://localhost:5001').");
		}

		if (!Uri.TryCreate(options.ServerAddress, UriKind.Absolute, out var uri))
		{
			return ValidateOptionsResult.Fail($"ServerAddress '{options.ServerAddress}' is not a valid absolute URI.");
		}

		if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
		{
			return ValidateOptionsResult.Fail($"ServerAddress scheme '{uri.Scheme}' is not supported. Use 'http' or 'https'.");
		}

		if (options.MaxSendMessageSize is < 1)
		{
			return ValidateOptionsResult.Fail("MaxSendMessageSize must be at least 1 byte when specified.");
		}

		if (options.MaxReceiveMessageSize is < 1)
		{
			return ValidateOptionsResult.Fail("MaxReceiveMessageSize must be at least 1 byte when specified.");
		}

		return ValidateOptionsResult.Success;
	}
}
