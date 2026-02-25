// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for messages that support correlation tracking.
/// </summary>
public interface ICorrelationAware
{
	/// <summary>
	/// Gets or sets the correlation identifier for tracking related messages.
	/// </summary>
	/// <value> The correlation identifier or <see langword="null" /> when not assigned. </value>
	Guid? CorrelationId { get; set; }
}
