// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// The ambient mode: the tenant a host established for the current request or message.
/// </summary>
internal sealed class AmbientTenantContextMode : ITenantContextMode
{
	/// <inheritdoc />
	public int Precedence => 1;

	/// <inheritdoc />
	public ITenantContext Create(IServiceProvider services) => new AmbientTenantContext();
}
