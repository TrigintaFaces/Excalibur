// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Commands;

namespace RemoteBusSample;

// Convention-based: ActivityDisplayName = "Ping", ActivityDescription = "RemoteBusSample: Ping"
public sealed class PingCommand : CommandBase<string>
{
	public string Text { get; init; } = string.Empty;
}
