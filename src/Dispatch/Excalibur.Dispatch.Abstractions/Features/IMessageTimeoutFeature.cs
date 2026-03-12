// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for message timeout tracking.
/// </summary>
public interface IMessageTimeoutFeature
{
	/// <summary>
	/// Gets or sets a value indicating whether processing exceeded the configured timeout.
	/// </summary>
	/// <value><see langword="true"/> if timeout exceeded; otherwise, <see langword="false"/>.</value>
	bool TimeoutExceeded { get; set; }

	/// <summary>
	/// Gets or sets the elapsed time before timeout occurred.
	/// </summary>
	/// <value>The elapsed time, or <see langword="null"/> if no timeout.</value>
	TimeSpan? TimeoutElapsed { get; set; }
}
