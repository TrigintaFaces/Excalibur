// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Rectification;

/// <summary>
/// Represents an auditable record of a data rectification that was performed.
/// </summary>
/// <param name="SubjectId">The data subject identifier.</param>
/// <param name="FieldName">The name of the field that was rectified.</param>
/// <param name="OldValue">The value before rectification.</param>
/// <param name="NewValue">The value after rectification.</param>
/// <param name="Reason">The reason the rectification was performed.</param>
/// <param name="RectifiedAt">The timestamp when the rectification occurred.</param>
public sealed record RectificationRecord(
	string SubjectId,
	string FieldName,
	string OldValue,
	string NewValue,
	string Reason,
	DateTimeOffset RectifiedAt);
