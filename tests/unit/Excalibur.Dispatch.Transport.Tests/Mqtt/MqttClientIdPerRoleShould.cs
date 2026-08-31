// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Mqtt;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// CI-runnable regression lock (no broker) for the per-role MQTT client-id distinctness fix: the
/// publisher and subscriber connect with DISTINCT client ids, or an MQTT broker (one session per client
/// id) evicts one connection — dropping the subscription and losing messages. The behavioral round-trip
/// lives in the container-gated conformance suite (soft-skipped without a broker); this is the cheap
/// always-running backstop.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Transport", "Mqtt")]
public sealed class MqttClientIdPerRoleShould
{
	private static MqttConnectionProvider Provider() =>
		// RequireTls opted out deliberately: this fixture exercises client-id construction against a
		// plaintext local broker. The posture itself is locked in TransportTlsPostureShould.
		new(new MqttOptions { ClientId = "svc", Host = "localhost", Port = 1883, RequireTls = false });

	[Fact]
	public void BuildDistinctClientIdsForPublisherAndSubscriberRoles()
	{
		var provider = Provider();

		var pub = provider.BuildClientOptions("pub");
		var sub = provider.BuildClientOptions("sub");

		pub.ClientId.ShouldBe("svc-pub");
		sub.ClientId.ShouldBe("svc-sub");

		// Load-bearing: RED on the pre-fix impl that presented the same client id for both roles — the
		// broker would evict one connection, dropping the subscription and losing messages.
		pub.ClientId.ShouldNotBe(sub.ClientId);
	}

	[Fact]
	public void SuffixTheConfiguredClientIdWithTheRoleDiscriminator()
	{
		Provider().BuildClientOptions("sub").ClientId.ShouldBe("svc-sub");
	}
}
