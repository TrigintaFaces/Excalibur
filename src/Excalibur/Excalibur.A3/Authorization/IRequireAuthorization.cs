// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Base interface for dispatch messages that require authorization to execute.
/// </summary>
public interface IRequireAuthorization : IDispatchAction
{
	/// <summary>
	/// Gets the unique activity name used to represent this action or permission (e.g., "Orders.Create").
	/// </summary>
	/// <value>The unique activity name.</value>
	string ActivityName { get; }
}
