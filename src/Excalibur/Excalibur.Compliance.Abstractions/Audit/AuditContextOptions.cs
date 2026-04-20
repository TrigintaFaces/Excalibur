// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Compliance;

/// <summary>
/// Configuration options for the scoped <see cref="IAuditContext"/>.
/// </summary>
public sealed class AuditContextOptions
{
	/// <summary>
	/// Gets or sets the default event type when not specified by the handler.
	/// </summary>
	public AuditEventType DefaultEventType { get; set; } = AuditEventType.Compliance;

	/// <summary>
	/// Gets or sets a value indicating whether to include the message type name
	/// as metadata on all assertions.
	/// </summary>
	public bool IncludeMessageTypeName { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of assertions per handler execution.
	/// Excess assertions are logged as a warning and dropped (never thrown).
	/// </summary>
	[Range(1, 1000)]
	public int MaxAssertionsPerScope { get; set; } = 25;
}
