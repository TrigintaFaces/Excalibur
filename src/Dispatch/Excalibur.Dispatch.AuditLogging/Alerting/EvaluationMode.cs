// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Alerting;

/// <summary>
/// Controls when audit alert rules are evaluated.
/// </summary>
public enum EvaluationMode
{
	/// <summary>Evaluate rules immediately on each audit event.</summary>
	RealTime = 0,

	/// <summary>Evaluate rules in periodic batches.</summary>
	Batch = 1
}
