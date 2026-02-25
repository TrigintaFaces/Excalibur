// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// State of adaptation for adaptive patterns.
/// </summary>
public enum AdaptationState
{
	/// <summary>
	/// System is stable and no adaptation is currently needed.
	/// </summary>
	Stable = 0,

	/// <summary>
	/// System is currently adapting to changed conditions.
	/// </summary>
	Adapting = 1,

	/// <summary>
	/// System is monitoring conditions for adaptation opportunities.
	/// </summary>
	Monitoring = 2,
}
