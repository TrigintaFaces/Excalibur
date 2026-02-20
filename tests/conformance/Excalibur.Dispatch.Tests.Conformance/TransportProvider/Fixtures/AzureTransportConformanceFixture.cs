// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Conformance fixture for Azure transport implementations.
/// </summary>
public sealed class AzureTransportConformanceFixture : TransportConformanceFixtureBase
{
	/// <inheritdoc />
	public override string TransportName => "Azure";

	/// <inheritdoc />
	protected override ITransportTestHarness CreateHarness()
	{
		return base.CreateHarness();
	}
}
