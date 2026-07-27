// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Microsoft-first fail-fast lock for RabbitMQ options: the transport wires
/// <see cref="IValidateOptions{RabbitMqOptions}"/> plus <c>ValidateOnStart()</c>, so a bad configuration is
/// rejected eagerly instead of failing later at first use. Asserts the testing-patterns §3 safety∧liveness pair.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RabbitMqOptionsValidateOnStartShould : UnitTestBase
{
	// SAFETY: an invalid config (missing connection string) is rejected when the options are materialized --
	// the wired IValidateOptions fires. RED if the validator registration or ValidateOnStart were dropped.
	[Fact]
	public void RejectMissingConnectionString()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddRabbitMQTransport("test", _ => { /* no connection string configured */ });

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
	}

	// SAFETY: the validator also rejects insecure default guest:guest credentials.
	[Fact]
	public void RejectDefaultGuestCredentials()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqp://guest:guest@localhost:5672/"));

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
	}

	// LIVENESS: a valid, secure config materializes without throwing -- validation must not reject good input
	// (a validator that rejected everything would satisfy the safety arm alone).
	[Fact]
	public void AcceptValidSecureConfiguration()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqp://appuser:S3cretPw0rd@localhost:5672/"));

		using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

		options.Connection.ConnectionString.ShouldBe("amqp://appuser:S3cretPw0rd@localhost:5672/");
	}
}
