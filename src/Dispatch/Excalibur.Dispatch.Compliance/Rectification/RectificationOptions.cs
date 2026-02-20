// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Rectification;

/// <summary>
/// Configuration options for the data rectification service.
/// </summary>
public sealed class RectificationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether rectifications require
	/// approval before being applied.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to require approval before applying rectifications;
	/// otherwise, <see langword="false"/>. Default is <see langword="false"/>.
	/// </value>
	public bool RequireApproval { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether all rectification changes
	/// should be recorded in the audit log.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to audit all rectification changes;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool AuditAllChanges { get; set; } = true;
}
