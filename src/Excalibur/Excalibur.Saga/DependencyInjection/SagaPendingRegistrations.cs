// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// Instance-scoped accumulator for saga type and dispatch delegate registrations
/// discovered during DI composition. Replaces the previous static ConcurrentBag fields
/// to prevent test contamination across parallel test runs.
/// </summary>
/// <remarks>
/// Registered as a singleton in the DI container. Read by
/// <see cref="Microsoft.Extensions.DependencyInjection.SagaServiceCollectionExtensions"/>
/// populators on first options resolution.
/// </remarks>
internal sealed class SagaPendingRegistrations
{
	/// <summary>
	/// Saga and state types accumulated during DI composition.
	/// Read by SagaTypeRegistryPopulator on first options resolution.
	/// </summary>
	public ConcurrentBag<Type> TypeRegistrations { get; } = [];

	/// <summary>
	/// Dispatch delegate registrations accumulated during DI composition.
	/// Read by SagaDispatchRegistryPopulator on first options resolution.
	/// </summary>
	public ConcurrentBag<Action<ISagaDispatchRegistry>> DispatchRegistrations { get; } = [];
}
