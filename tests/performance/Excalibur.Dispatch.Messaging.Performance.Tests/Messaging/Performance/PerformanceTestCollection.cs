// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Messaging.Performance;

/// <summary>
/// Collection definition for performance tests.
/// All tests in this collection run sequentially to avoid resource contention
/// and timing interference under full-suite VS Test Explorer load.
/// </summary>
/// <remarks>
/// While <c>xunit.runner.json</c> already disables parallelization assembly-wide,
/// this explicit definition provides defense-in-depth and documents the intent.
/// </remarks>
[CollectionDefinition("Performance Tests", DisableParallelization = true)]
public sealed class PerformanceTestCollection;
