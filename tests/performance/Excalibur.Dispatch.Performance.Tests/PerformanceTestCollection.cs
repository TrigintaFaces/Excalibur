// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// Collection definition for performance-sensitive tests.
/// All tests in this collection run sequentially to avoid resource contention from parallel execution.
/// This prevents flaky failures in tests that depend on CPU timing, GC behavior, or throughput metrics.
/// </summary>
[CollectionDefinition("Performance Tests", DisableParallelization = true)]
public sealed class PerformanceTestCollection;
