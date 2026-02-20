// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Conformance fixture for Google Cloud transport implementations.
/// </summary>
/// <remarks>
///     TODO(TASK-GOOGLE-TRANSPORT-COMPLETE): Google Transport is currently disabled due to compilation issues.
///     This fixture is a placeholder until the implementation is completed.
/// </remarks>
public sealed class GoogleTransportConformanceFixture : TransportConformanceFixtureBase
{
	/// <inheritdoc />
	public override string TransportName => "Google";

	/// <inheritdoc />
	protected override ITransportTestHarness CreateHarness()
	{
		// TODO(TASK-GOOGLE-TRANSPORT-COMPLETE): Re-enable when Google Transport is implemented
		// _ = typeof(GooglePubSubTransportServiceCollectionExtensions);
		return base.CreateHarness();
	}
}
