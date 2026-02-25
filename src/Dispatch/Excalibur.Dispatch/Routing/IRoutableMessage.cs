// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Interface for routable messages.
/// </summary>
public interface IRoutableMessage
{
	/// <summary>
	/// Get a key for route caching.
	/// </summary>
	int GetRouteKey();
}
