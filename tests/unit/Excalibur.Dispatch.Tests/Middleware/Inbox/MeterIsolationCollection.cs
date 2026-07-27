// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Tests.Middleware.Inbox;

/// <summary>
/// 2ivlcy: serializes <see cref="System.Diagnostics.Metrics.MeterListener"/>-based capture tests so they
/// never run concurrently with any other collection. This assembly runs test collections in parallel
/// (<c>parallelizeTestCollections: true</c>), but a <c>MeterListener</c> and the process-global metrics
/// instruments it captures are shared per-process — concurrent meter-capture classes contend on the same
/// instrument publish/enable lifecycle, which under full-shard parallel load intermittently drops the
/// under-test measurement (passes 3/3 isolated, flakes under load). Placing a meter-capture class in this
/// <c>DisableParallelization</c> collection makes its capture window exclusive (enforce-invariants-structurally:
/// the contention is inexpressible because the tests cannot run at the same time).
/// </summary>
[CollectionDefinition("Meter Isolation", DisableParallelization = true)]
public sealed class MeterIsolationCollection;
