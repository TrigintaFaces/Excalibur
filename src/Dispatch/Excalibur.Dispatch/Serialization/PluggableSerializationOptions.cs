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
	/// If null, no serializer is set as current. Use <c>builder.UseCurrent("System.Text.Json")</c>
	/// or other format-specific extensions to explicitly set the current serializer.
	/// </remarks>
	public string? CurrentSerializerName { get; set; }

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
