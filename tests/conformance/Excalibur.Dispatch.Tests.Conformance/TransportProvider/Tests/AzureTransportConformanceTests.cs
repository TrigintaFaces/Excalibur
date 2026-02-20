// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Tests;

public sealed class AzureTransportConformanceTests(AzureTransportConformanceFixture fixture) : TransportConformanceSpecification<AzureTransportConformanceFixture>(fixture)
{
}
