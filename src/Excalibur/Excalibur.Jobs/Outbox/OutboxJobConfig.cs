// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Core;

namespace Excalibur.Jobs.Outbox;

/// <summary>
/// Represents the configuration for the Outbox Job.
/// </summary>
/// <remarks>
/// Inherits common job configuration properties from <see cref="JobConfig" /> and can be extended with Outbox-specific settings if needed.
/// </remarks>
public sealed class OutboxJobConfig : JobConfig;
