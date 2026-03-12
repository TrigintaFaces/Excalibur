// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for message validation state.
/// </summary>
public interface IMessageValidationFeature
{
	/// <summary>
	/// Gets or sets a value indicating whether validation passed for this message.
	/// </summary>
	/// <value><see langword="true"/> if validation passed; otherwise, <see langword="false"/>.</value>
	bool ValidationPassed { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when validation completed.
	/// </summary>
	/// <value>The validation completion timestamp, or <see langword="null"/> if not validated.</value>
	DateTimeOffset? ValidationTimestamp { get; set; }
}
