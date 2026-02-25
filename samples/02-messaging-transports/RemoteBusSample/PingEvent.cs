// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace RemoteBusSample;

/// <summary>
/// Integration event for remote bus communication.
/// </summary>
/// <param name="Message"> Content of the message. </param>
public sealed record PingEvent(string Message) : IIntegrationEvent;
