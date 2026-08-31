// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Runs the shared <see cref="ITransportHealthChecker" /> conformance suite against <see cref="InMemoryTransportAdapter" />.
/// </summary>
public sealed class InMemoryTransportAdapterHealthCheckerConformanceShould : TransportHealthCheckerConformanceTests
{
	protected override string ExpectedHealthCheckerName => InMemoryTransportAdapter.DefaultName;

	protected override string ExpectedTransportType => InMemoryTransportAdapter.TransportTypeName;

	protected override TransportHealthCheckCategory ExpectedCategories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;

	protected override ITransportHealthChecker CreateHealthChecker() =>
		new InMemoryTransportAdapter(NullLogger<InMemoryTransportAdapter>.Instance);
}
