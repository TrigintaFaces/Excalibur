// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace MinimalSample;

/// <summary>
/// A simple ping command that returns a pong response.
/// Commands represent intent to perform an action and may return a result.
/// </summary>
public sealed record PingCommand : IDispatchAction<string>
{
	/// <summary>
	/// Gets or initializes the text to include in the ping.
	/// </summary>
	public string Text { get; init; } = string.Empty;
}
