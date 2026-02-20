// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Conformance fixture for Kafka transport implementations.
/// </summary>
public sealed class KafkaTransportConformanceFixture : TransportConformanceFixtureBase
{
	/// <inheritdoc />
	public override string TransportName => "Kafka";

	/// <inheritdoc />
	protected override ITransportTestHarness CreateHarness()
	{
		// Reference the new ADR-098 compliant entry point extensions
		_ = typeof(KafkaTransportServiceCollectionExtensions);
		return base.CreateHarness();
	}
}
