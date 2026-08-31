// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Confluent.Kafka;


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// The single place the Kafka transport decides whether a configured security protocol is acceptable.
/// </summary>
/// <remarks>
/// <para>
/// Every Kafka client this package builds -- producer, consumer, admin client, dead-letter consumer,
/// health-check client -- is built from a <see cref="ClientConfig"/>, and every one of those configurations
/// passes through <see cref="Apply{TConfig}"/>. That is what makes the posture reachable rather than
/// merely implemented: a client cannot be constructed without a configuration, and a configuration
/// cannot be produced without this check having run.
/// </para>
/// <para>
/// The protocol has two spellings -- the typed <see cref="KafkaOptions.SecurityProtocol"/> and the raw
/// <c>security.protocol</c> key in <see cref="KafkaOptions.AdditionalConfig"/>. Rather than let one
/// silently win, a disagreement between them is refused: silent precedence between two ways of setting
/// the same security control is how an intended TLS posture becomes a plaintext connection nobody
/// notices.
/// </para>
/// </remarks>
internal static class KafkaSecurityPosture
{
	/// <summary>
	/// The librdkafka configuration key that carries the security protocol as a raw string.
	/// </summary>
	internal const string SecurityProtocolConfigKey = "security.protocol";

	/// <summary>
	/// The transport this posture speaks for, stamped onto every refusal it raises so a host wiring more
	/// than one transport can tell which one refused.
	/// </summary>
	internal const string TransportLabel = "Kafka";

	/// <summary>
	/// Gets a value indicating whether the supplied protocol carries TLS.
	/// </summary>
	/// <param name="protocol">The protocol to test, or <see langword="null"/> when none is configured.</param>
	/// <returns><see langword="true"/> when the protocol encrypts the wire; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// An unset protocol is plaintext at the wire -- librdkafka defaults to <c>plaintext</c> -- and is
	/// treated as such here. "Not configured" is never given the benefit of the doubt.
	/// </remarks>
	internal static bool IsTls(SecurityProtocol? protocol) =>
		protocol is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl;

	/// <summary>
	/// Resolves the effective security protocol from the typed property and the raw configuration key.
	/// </summary>
	/// <param name="options">The Kafka options to read.</param>
	/// <returns>The effective protocol, or <see langword="null"/> when neither spelling supplies one.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	/// <exception cref="TransportSecurityException">
	/// Thrown when the raw configuration value is not a recognized protocol, or when it contradicts the
	/// typed property. Both are refused rather than resolved, because either outcome would mean guessing
	/// at a security control.
	/// </exception>
	internal static SecurityProtocol? ResolveProtocol(KafkaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var typed = options.SecurityProtocol;

		if (!options.AdditionalConfig.TryGetValue(SecurityProtocolConfigKey, out var raw)
			|| string.IsNullOrWhiteSpace(raw))
		{
			return typed;
		}

		if (!TryParseProtocol(raw, out var fromRawConfig))
		{
			throw new TransportSecurityException(
				$"Cannot configure the Kafka client: '{SecurityProtocolConfigKey}' is set to '{raw}', which is not a "
				+ "recognized Kafka security protocol. Use one of 'plaintext', 'ssl', 'sasl_plaintext' or 'sasl_ssl', "
				+ "or set KafkaOptions.SecurityProtocol instead. The value is refused rather than ignored, because "
				+ "ignoring it would connect on whichever protocol librdkafka defaults to.")
			{
				TransportName = TransportLabel,
			};
		}

		if (typed is null || typed == fromRawConfig)
		{
			return fromRawConfig;
		}

		throw new TransportSecurityException(
			$"Cannot configure the Kafka client: the security protocol is set twice and the two disagree. "
			+ $"KafkaOptions.SecurityProtocol is '{typed}' while '{SecurityProtocolConfigKey}' in AdditionalConfig "
			+ $"is '{raw}'. Neither is preferred over the other -- set only one, because a silent winner between "
			+ "two spellings of a security control is how an intended TLS posture becomes a plaintext connection.")
		{
			TransportName = TransportLabel,
		};
	}

	/// <summary>
	/// Enforces the configured TLS posture and stamps the resolved protocol onto a client configuration.
	/// </summary>
	/// <typeparam name="TConfig">The Confluent client configuration type.</typeparam>
	/// <param name="config">The configuration about to be handed to a client builder.</param>
	/// <param name="options">The Kafka options carrying the posture and the protocol.</param>
	/// <returns>The same configuration, with the resolved security protocol applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
	/// <exception cref="TransportSecurityException">
	/// Thrown when <see cref="KafkaOptions.RequireTls"/> is set and the effective protocol does not carry TLS.
	/// </exception>
	internal static TConfig Apply<TConfig>(TConfig config, KafkaOptions options)
		where TConfig : ClientConfig
	{
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(options);

		var protocol = ResolveProtocol(options);

		if (options.RequireTls && !IsTls(protocol))
		{
			throw Refuse(protocol);
		}

		if (protocol is { } resolved)
		{
			config.SecurityProtocol = resolved;
		}

		return config;
	}

	/// <summary>
	/// Builds the refusal raised when TLS is required and the configured protocol cannot carry it.
	/// </summary>
	/// <param name="protocol">The effective protocol, or <see langword="null"/> when none is configured.</param>
	/// <returns>The exception to throw.</returns>
	/// <remarks>
	/// Shared so that every refusal in this package -- whichever client is being built -- says the same
	/// thing about the same condition.
	/// </remarks>
	internal static TransportSecurityException Refuse(SecurityProtocol? protocol) =>
		new($"Cannot establish the Kafka connection: TLS is required but the configured security protocol "
			+ $"is '{protocol?.ToString() ?? "Plaintext (not configured)"}', which carries credentials and message "
			+ "payloads in the clear. Set the security protocol to Ssl or SaslSsl, or set "
			+ "KafkaOptions.RequireTls to false to accept an unencrypted broker connection.")
		{
			TransportName = TransportLabel,
			FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
		};

	/// <summary>
	/// Parses a librdkafka protocol string such as <c>sasl_ssl</c> into the Confluent enum.
	/// </summary>
	private static bool TryParseProtocol(string raw, out SecurityProtocol protocol)
	{
		protocol = default;

		var trimmed = raw.Trim();

		// Enum.TryParse accepts bare numbers, which would let "1" mean a protocol nobody typed.
		if (trimmed.Length == 0 || char.IsDigit(trimmed[0]) || trimmed[0] is '-' or '+')
		{
			return false;
		}

		return Enum.TryParse(trimmed.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out protocol)
			&& Enum.IsDefined(protocol);
	}
}
