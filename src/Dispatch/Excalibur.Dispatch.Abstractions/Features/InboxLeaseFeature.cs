// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Features;

/// <summary>
/// Default implementation of <see cref="IInboxLeaseFeature"/>.
/// </summary>
public sealed class InboxLeaseFeature : IInboxLeaseFeature
{
	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? HandlerType { get; set; }

	/// <inheritdoc />
	public LeaseToken? Lease { get; set; }

	/// <inheritdoc />
	public InboxLeaseDisposition Disposition { get; set; }
}
