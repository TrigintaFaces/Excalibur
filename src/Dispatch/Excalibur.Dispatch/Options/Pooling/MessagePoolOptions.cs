// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Pooling.Configuration;

namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Configuration for message pools.
/// </summary>
public sealed class MessagePoolOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable message pooling.
	/// </summary>
	/// <value> The current <see cref="Enabled" /> value. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum pool size per message type.
	/// </summary>
	/// <value> The current <see cref="MaxPoolSizePerType" /> value. </value>
	public int MaxPoolSizePerType { get; set; } = Environment.ProcessorCount * 8;

	/// <summary>
	/// Gets or sets a value indicating whether to enable aggressive pooling.
	/// </summary>
	/// <value> The current <see cref="AggressivePooling" /> value. </value>
	public bool AggressivePooling { get; set; } = true;

	/// <summary>
	/// Gets type-specific configurations.
	/// </summary>
	/// <value> The current <see cref="TypeConfigurations" /> value. </value>
	public Dictionary<string, TypePoolOptions> TypeConfigurations { get; } = [];

	/// <summary>
	/// Gets or sets the default reset strategy.
	/// </summary>
	/// <value> The current <see cref="DefaultResetStrategy" /> value. </value>
	public ResetStrategy DefaultResetStrategy { get; set; } = ResetStrategy.Auto;

	/// <summary>
	/// Gets or sets trim behavior under memory pressure.
	/// </summary>
	/// <value> The current <see cref="TrimBehavior" /> value. </value>
	public TrimBehavior TrimBehavior { get; set; } = TrimBehavior.Adaptive;

	/// <summary>
	/// Gets or sets the maximum number of message types to track.
	/// </summary>
	/// <value> The current <see cref="MaxTrackedTypes" /> value. </value>
	public int MaxTrackedTypes { get; set; } = 100;
}
