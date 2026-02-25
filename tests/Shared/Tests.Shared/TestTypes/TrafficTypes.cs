// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Traffic routing rule for test scenarios.
/// </summary>
public class TrafficRule
{
	/// <summary>Gets or sets the rule name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the weight (0-100).</summary>
	public int Weight { get; set; } = 100;

	/// <summary>Gets or sets the target endpoint.</summary>
	public string Target { get; set; } = string.Empty;

	/// <summary>Gets or sets whether the rule is enabled.</summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>Gets or sets the priority.</summary>
	public int Priority { get; set; }
}

/// <summary>
/// Traffic split configuration for A/B testing.
/// </summary>
public class TrafficSplit
{
	/// <summary>Gets or sets the name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the splits.</summary>
	public List<TrafficRule> Splits { get; set; } = new();

	/// <summary>Gets or sets the total weight.</summary>
	public int TotalWeight => Splits.Sum(s => s.Weight);
}

/// <summary>
/// Traffic configuration for routing scenarios.
/// </summary>
public class TrafficConfiguration
{
	/// <summary>Gets or sets the configuration name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the rules.</summary>
	public List<TrafficRule> Rules { get; set; } = new();

	/// <summary>Gets or sets the splits.</summary>
	public List<TrafficSplit> Splits { get; set; } = new();

	/// <summary>Gets or sets the default target.</summary>
	public string DefaultTarget { get; set; } = string.Empty;
}
