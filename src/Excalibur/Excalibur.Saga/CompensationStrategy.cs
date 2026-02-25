// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga;

/// <summary>
/// Specifies the compensation strategy for a saga step when compensation fails.
/// </summary>
public enum CompensationStrategy
{
	/// <summary>
	/// Use the <see cref="AdvancedSagaOptions.EnableAutoCompensation"/> setting to determine behavior.
	/// </summary>
	Default = 0,

	/// <summary>
	/// Retry compensation up to <see cref="Attributes.SagaCompensationAttribute.MaxRetries"/> times, then fail.
	/// </summary>
	Retry = 1,

	/// <summary>
	/// Log the compensation failure and skip this compensation step.
	/// </summary>
	Skip = 2,

	/// <summary>
	/// Mark the saga as requiring manual intervention.
	/// </summary>
	ManualIntervention = 3,
}
