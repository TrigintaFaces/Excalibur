// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Validates <see cref="RequestHeaderCaptureOptions"/> at startup, refusing a configuration that
/// names a credential-bearing header or sets a cap that could never admit an entry.
/// </summary>
/// <remarks>
/// <para>
/// Both failures are dangerous in the quiet direction. A credential header named in the allowlist would
/// be copied onto every outbound message and into at least one durable store, and nothing at runtime
/// would report it. A non-positive cap drops every header while the configuration reads as though
/// propagation were enabled, which is indistinguishable at runtime from an allowlist nobody filled in.
/// Startup is where both can still be acted on.
/// </para>
/// <para>
/// Refusing at startup rather than skipping at runtime is also the cheaper control: it runs once per
/// process and costs nothing per message, where a runtime filter would be paid by every consumer on
/// every request forever.
/// </para>
/// </remarks>
internal sealed class RequestHeaderCaptureOptionsValidator
	: IValidateOptions<RequestHeaderCaptureOptions>
{
	/// <summary>
	/// The headers that carry caller credentials and are refused in the allowlist.
	/// </summary>
	/// <remarks>
	/// This is a backstop on an allowlist, not a filter, and it is deliberately short: it names the
	/// credential headers defined by HTTP itself. A bespoke scheme — an API key in <c>X-Api-Key</c>, a
	/// token in a vendor header — is indistinguishable here from a request identifier, so naming one
	/// remains the application's decision and its consequence. <c>Set-Cookie</c> is absent on purpose:
	/// it is a response header and cannot arrive on a request, so refusing it would be configuration
	/// that can never fire.
	/// </remarks>
	private static readonly string[] CredentialHeaders =
	[
		"Authorization",
		"Cookie",
		"Proxy-Authorization",
	];

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RequestHeaderCaptureOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(RequestHeaderCaptureOptions)} must not be null.");
		}

		var failures = new List<string>();

		foreach (var credential in CredentialHeaders)
		{
			if (options.AllowedHeaders.Contains(credential))
			{
				failures.Add(
					$"{nameof(RequestHeaderCaptureOptions.AllowedHeaders)} must not contain '{credential}'. "
					+ "That header carries the caller's credentials, and a captured header is placed on the wire "
					+ "by every remote transport and persisted by at least one outbox provider — so allowing it "
					+ "forwards the credential to every downstream system and stores it. If you need to act on "
					+ "the caller's identity downstream, carry a claim or a token you minted for that purpose "
					+ "rather than the inbound credential.");
			}
		}

		if (options.MaxEntries <= 0)
		{
			failures.Add(
				$"{nameof(RequestHeaderCaptureOptions.MaxEntries)} must be greater than zero; "
				+ $"{options.MaxEntries} would drop every header while the allowlist still reads as though "
				+ "names were permitted. To copy nothing, leave AllowedHeaders empty — that is already the "
				+ "default.");
		}

		if (options.MaxValueLength <= 0)
		{
			failures.Add(
				$"{nameof(RequestHeaderCaptureOptions.MaxValueLength)} must be greater than zero; "
				+ $"{options.MaxValueLength} would drop every value, including the ones you allowed by name.");
		}

		if (options.MaxTotalLength <= 0)
		{
			failures.Add(
				$"{nameof(RequestHeaderCaptureOptions.MaxTotalLength)} must be greater than zero; "
				+ $"{options.MaxTotalLength} would drop every header regardless of the other caps.");
		}

		if (options.AllowedHeaders.Any(string.IsNullOrWhiteSpace))
		{
			failures.Add(
				$"{nameof(RequestHeaderCaptureOptions.AllowedHeaders)} must not contain a blank name. A "
				+ "blank entry matches no header and is almost always a configuration binding that lost its "
				+ "value.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
