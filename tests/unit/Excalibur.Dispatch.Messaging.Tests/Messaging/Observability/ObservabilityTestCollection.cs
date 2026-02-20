// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Messaging.Observability;

/// <summary>
/// Collection definition for observability tests.
/// All tests in this collection run sequentially to avoid interference from shared static ActivitySource listeners.
/// </summary>
[CollectionDefinition("Observability Tests", DisableParallelization = true)]
public sealed class ObservabilityTestCollection;
