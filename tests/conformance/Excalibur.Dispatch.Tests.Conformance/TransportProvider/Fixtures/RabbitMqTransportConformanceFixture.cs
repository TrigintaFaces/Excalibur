// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Conformance fixture for RabbitMQ transport implementations.
/// </summary>
public sealed class RabbitMqTransportConformanceFixture : TransportConformanceFixtureBase
{
	/// <inheritdoc />
	public override string TransportName => "RabbitMQ";

	/// <inheritdoc />
	protected override ITransportTestHarness CreateHarness()
	{
		// Reference the new ADR-098 compliant entry point extensions
		_ = typeof(RabbitMQTransportServiceCollectionExtensions);
		return base.CreateHarness();
	}
}
