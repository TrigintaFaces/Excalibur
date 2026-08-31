// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// The Azure Event Hubs transport is registered under a name while its runtime options were registered
/// without one, so two named Event Hubs transports in one container wrote the same options instance and
/// the second silently replaced the first. Nothing threw and nothing logged: the losing transport ran
/// against the winner's hub and consumer group.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AzureEventHubsNamedOptionsShould
{
}
