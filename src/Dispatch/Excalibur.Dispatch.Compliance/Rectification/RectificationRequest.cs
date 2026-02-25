// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Rectification;

/// <summary>
/// Represents a request to rectify personal data under GDPR Article 16.
/// </summary>
/// <param name="SubjectId">The data subject identifier whose data is being rectified.</param>
/// <param name="FieldName">The name of the field being rectified.</param>
/// <param name="OldValue">The current (inaccurate) value of the field.</param>
/// <param name="NewValue">The corrected value for the field.</param>
/// <param name="Reason">The reason for the rectification.</param>
public sealed record RectificationRequest(
	string SubjectId,
	string FieldName,
	string OldValue,
	string NewValue,
	string Reason);
