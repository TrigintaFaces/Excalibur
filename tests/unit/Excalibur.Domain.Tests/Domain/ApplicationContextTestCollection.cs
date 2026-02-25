// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// xUnit test collection that serializes test classes which mutate the static
/// <see cref="ApplicationContext"/> state.
/// Without serialization, parallel test classes can leak static state between each other.
/// </summary>
/// <remarks>
/// Sprint 537 (S537.1): Fixes bd-k4ei7 — ApplicationContextShould.Init_SkipsSecureStorage_WhenSensitiveValueIsEmpty
/// failed under parallel execution because ActivityContextExtensionsShould also calls ApplicationContext.Init().
/// </remarks>
[CollectionDefinition("ApplicationContext", DisableParallelization = true)]
public sealed class ApplicationContextTestCollection;
