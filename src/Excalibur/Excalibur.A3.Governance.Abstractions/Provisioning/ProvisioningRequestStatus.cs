// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Represents the lifecycle state of a provisioning request.
/// </summary>
public enum ProvisioningRequestStatus
{
	/// <summary>
	/// The request has been created but not yet submitted for review.
	/// </summary>
	Pending = 0,

	/// <summary>
	/// The request is under review by one or more approvers.
	/// </summary>
	InReview = 1,

	/// <summary>
	/// All required approval steps have been approved.
	/// </summary>
	Approved = 2,

	/// <summary>
	/// One or more required approval steps were denied.
	/// </summary>
	Denied = 3,

	/// <summary>
	/// The approved grant has been successfully provisioned.
	/// </summary>
	Provisioned = 4,

	/// <summary>
	/// The provisioning of the approved grant failed.
	/// </summary>
	Failed = 5,
}
