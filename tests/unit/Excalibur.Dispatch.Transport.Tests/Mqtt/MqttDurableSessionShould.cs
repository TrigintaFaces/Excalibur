// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.Mqtt;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Locks the session settings that decide whether rejecting a message means anything on MQTT.
/// </summary>
/// <remarks>
/// <para>
/// The receiver rejects a message by withholding its acknowledgement, which is a promise that the broker
/// still holds it and will redeliver it when the session resumes. Whether that promise is true is decided
/// entirely by two flags on the CONNECT packet, and the library defaults set both against it: a clean start
/// discards any prior session, and a zero expiry interval ends the session the moment the connection closes.
/// The reject call succeeded, the broker was healthy, and the message was gone.
/// </para>
/// <para>
/// Stated plainly, because it bounds what these arms are worth: they assert what this framework PUTS ON THE
/// WIRE, not that a broker honours it. Session resumption is the broker's behaviour and is specified by
/// OASIS, not by us; our entire contribution is which flags we send, so that is what is locked here. The
/// end-to-end proof — reject, disconnect, resume, receive it exactly once — needs a real broker and lives
/// with the container-gated conformance suite. These arms are the always-running backstop.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Transport", "Mqtt")]
public sealed class MqttDurableSessionShould
{
	private static MqttOptions Options() => new()
	{
		ClientId = "svc",
		Host = "localhost",
		Port = 1883,

		// Load-bearing, and it was missing: a refusal arm asserts that validation FAILS, so a fixture that is
		// invalid for any OTHER reason satisfies it without ever reaching the check under test. Leaving Topic unset
		// made the fractional-window arm pass on "Topic is required" -- green, and blind to its own subject.
		Topic = "orders/topic",

		// Plaintext deliberately: these arms are about session lifetime, and the TLS posture has its own lock.
		RequireTls = false,
	};

	/// <summary>
	/// SAFETY. The subscriber connects with a session that survives the disconnect.
	/// </summary>
	/// <remarks>
	/// RED against the shipped defaults, which is the whole point: MQTTnet leaves CleanStart true and the
	/// expiry interval at zero, so the session a rejected message depends on did not exist.
	/// </remarks>
	[Fact]
	public void KeepTheSessionThatRejectionDependsOn()
	{
		var options = new MqttConnectionProvider(Options()).BuildClientOptions("sub");

		options.CleanSession.ShouldBeFalse(
			"a clean start discards the prior session, so a message rejected before the disconnect is lost");
		options.SessionExpiryInterval.ShouldBe(3600u,
			"a zero expiry ends the session when the connection closes, leaving nothing to resume");
	}

	/// <summary>
	/// SAFETY. The publisher connection carries the same policy, so the setting is not role-dependent.
	/// </summary>
	[Fact]
	public void ApplyTheSessionPolicyToEveryRole()
	{
		var options = new MqttConnectionProvider(Options()).BuildClientOptions("pub");

		options.CleanSession.ShouldBeFalse();
		options.SessionExpiryInterval.ShouldBe(3600u);
	}

	/// <summary>
	/// LIVENESS. The policy is a real setting, not a constant written into the provider.
	/// </summary>
	/// <remarks>
	/// Without this arm, the arms above are satisfied by a provider that hard-codes a persistent session and
	/// ignores the consumer entirely — which would be a different dishonesty in the same place. A consumer who
	/// opts out must actually get a clean start, and the zero expiry must be written explicitly rather than
	/// left to a default that happens to agree today.
	/// </remarks>
	[Fact]
	public void HonourAConsumerWhoOptsOutOfSessionPersistence()
	{
		var options = Options();
		options.PersistentSession = false;

		var built = new MqttConnectionProvider(options).BuildClientOptions("sub");

		built.CleanSession.ShouldBeTrue();
		built.SessionExpiryInterval.ShouldBe(0u);
	}

	/// <summary>
	/// LIVENESS. A configured recovery window reaches the wire as configured.
	/// </summary>
	[Fact]
	public void CarryTheConfiguredRecoveryWindowToTheWire()
	{
		var options = Options();
		options.SessionExpiryInterval = TimeSpan.FromMinutes(30);

		new MqttConnectionProvider(options).BuildClientOptions("sub").SessionExpiryInterval.ShouldBe(1800u);
	}

	/// <summary>
	/// SAFETY. A persistent session with an immediate expiry is refused at startup.
	/// </summary>
	/// <remarks>
	/// This is the original defect expressed as configuration: it reads as "keep my session", it validates, and
	/// it behaves exactly like the clean start it claims to replace. Refusing it at startup is the only place
	/// the contradiction is visible.
	/// </remarks>
	[Fact]
	public void RefuseAPersistentSessionThatExpiresImmediately()
	{
		var options = Options();
		options.SessionExpiryInterval = TimeSpan.Zero;

		var result = new MqttOptionsValidator().Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(MqttOptions.SessionExpiryInterval));
	}

	/// <summary>
	/// SAFETY. A window that cannot be represented on the wire is refused, never rounded.
	/// </summary>
	/// <remarks>
	/// The wire encodes whole seconds, so the provider casts to a whole number. Silently truncating a consumer's
	/// 90.5-second window to 90 would hand them a recovery window they did not configure — the same class of
	/// defect as the default this option exists to correct, which is why it fails loudly instead.
	/// </remarks>
	[Fact]
	public void RefuseARecoveryWindowThatIsNotAWholeNumberOfSeconds()
	{
		var options = Options();
		options.SessionExpiryInterval = TimeSpan.FromMilliseconds(90_500);

		var result = new MqttOptionsValidator().Validate(null, options);

		result.Failed.ShouldBeTrue();

		// Naming the member is what makes this arm about its own subject. Asserting only that validation failed
		// is satisfied by any other invalid field, which is how this arm first passed against a mutant that
		// disabled the very check it is named for.
		result.FailureMessage.ShouldContain(nameof(MqttOptions.SessionExpiryInterval));
	}

	/// <summary>
	/// LIVENESS. The ordinary configuration is accepted.
	/// </summary>
	/// <remarks>
	/// The refusal arms above are all satisfied by a validator that rejects every configuration, which would
	/// take the transport out of service entirely.
	/// </remarks>
	[Fact]
	public void AcceptAWholeSecondRecoveryWindow()
	{
		var options = Options();
		options.SessionExpiryInterval = TimeSpan.FromMinutes(5);

		new MqttOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
	}
}
