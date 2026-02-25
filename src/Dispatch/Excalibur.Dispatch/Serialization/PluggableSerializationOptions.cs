// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Options for configuring pluggable serialization.
/// </summary>
/// <remarks>
/// <para>
/// These options are used to defer serializer registration until the DI container is built.
/// This allows for proper configuration without using <c>BuildServiceProvider()</c>.
/// </para>
/// <para>
/// Registration actions are collected during configuration and executed when the
/// <see cref="ISerializerRegistry"/> singleton is created.
/// </para>
/// </remarks>
public sealed class PluggableSerializationOptions
{
	/// <summary>
	/// Gets or sets the name of the serializer to set as current after registration.
	/// </summary>
	/// <remarks>
	/// If null, MemoryPack is used as the default.
	/// </remarks>
	public string? CurrentSerializerName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether MemoryPack should be auto-registered.
	/// </summary>
	/// <remarks>
	/// Defaults to true. Set to false to disable automatic MemoryPack registration.
	/// </remarks>
	public bool AutoRegisterMemoryPack { get; set; } = true;

	/// <summary>
	/// Gets the list of serializer registration actions to execute.
	/// </summary>
	internal List<Action<ISerializerRegistry>> RegistrationActions { get; } = [];

	/// <summary>
	/// Adds a serializer registration action.
	/// </summary>
	/// <param name="registrationAction">The action to execute during registry initialization.</param>
	internal void AddRegistration(Action<ISerializerRegistry> registrationAction)
	{
		ArgumentNullException.ThrowIfNull(registrationAction);
		RegistrationActions.Add(registrationAction);
	}
}
