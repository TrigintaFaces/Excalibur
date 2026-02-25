// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Conformance fixture for AWS transport implementations.
/// </summary>
public sealed class AwsTransportConformanceFixture : TransportConformanceFixtureBase
{
	/// <inheritdoc />
	public override string TransportName => "AWS";

	/// <inheritdoc />
	protected override ITransportTestHarness CreateHarness()
	{
		_ = typeof(AwsServiceCollectionExtensions);
		return base.CreateHarness();
	}
}
