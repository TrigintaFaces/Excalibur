// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Test collection for cache-related tests that require isolation from other tests
/// due to static cache state dependencies.
/// </summary>
/// <remarks>
/// Tests in this collection will not run in parallel with each other, ensuring
/// that static cache state is properly isolated between tests.
/// </remarks>
[CollectionDefinition("CacheTests", DisableParallelization = true)]
public sealed class CacheTestCollection;
