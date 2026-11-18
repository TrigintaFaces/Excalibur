// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>Describes whether a publishing operation finished its admitted work.</summary>
public enum PublishingOutcome
{
	/// <summary>The operation completed its normal workflow; row failures are reported separately.</summary>
	Completed = 0,
	/// <summary>Admission or continuation was refused for the captured leadership tenure.</summary>
	TenureRefused = 1,
	/// <summary>An infrastructure fault interrupted admission or completion of the operation.</summary>
	Interrupted = 2,
}
