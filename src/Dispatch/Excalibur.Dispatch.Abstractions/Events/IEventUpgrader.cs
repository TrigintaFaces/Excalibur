// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a component that can upgrade events from one version to another.
/// </summary>
/// <typeparam name="TFrom"> The source event type. </typeparam>
/// <typeparam name="TTo"> The target event type. </typeparam>
public interface IEventUpgrader<in TFrom, out TTo>
	where TFrom : IDomainEvent
	where TTo : IDomainEvent
{
	/// <summary>
	/// Upgrades an event from one version to another.
	/// </summary>
	/// <param name="fromEvent"> The event to upgrade. </param>
	/// <returns> The upgraded event. </returns>
	TTo Upgrade(TFrom fromEvent);
}
