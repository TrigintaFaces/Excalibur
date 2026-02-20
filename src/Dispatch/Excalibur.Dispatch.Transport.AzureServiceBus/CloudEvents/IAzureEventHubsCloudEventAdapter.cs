// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Messaging.EventHubs;


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Maps CloudEvents to <see cref="EventData" /> instances for Azure Event Hubs and vice versa./. </summary>
/// <remarks> This will be fully implemented in a future iteration. </remarks>
public interface IAzureEventHubsCloudEventAdapter : ICloudEventMapper<EventData>;
