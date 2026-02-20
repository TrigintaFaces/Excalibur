// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests;

/// <summary>
/// xUnit test collection that serializes test classes which mutate process-global
/// environment variables (K_SERVICE, FUNCTION_NAME, AWS_LAMBDA_FUNCTION_NAME, etc.).
/// Without serialization, parallel test classes can leak env var state between each other.
/// </summary>
/// <remarks>
/// Sprint 535 (S535.1): Fixes bd-863bd — GoogleCloudFunctionsHostProviderShould.IsAvailable_ReturnsTrue_WhenKServiceSet
/// failed under parallel execution because ServerlessHostProviderFactoryShould also sets K_SERVICE.
/// </remarks>
[CollectionDefinition("EnvironmentVariableTests", DisableParallelization = true)]
public sealed class EnvironmentVariableTestCollection;
