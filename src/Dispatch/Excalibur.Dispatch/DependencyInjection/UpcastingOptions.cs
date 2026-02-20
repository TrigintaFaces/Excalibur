// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Versioning;

/// <summary>
/// Options for configuring message upcasting services.
/// </summary>
/// <remarks>
/// <para>
/// This class collects registration actions during configuration. The actions are executed
/// when the <see cref="IUpcastingPipeline"/> singleton is created.
/// </para>
/// <para>
/// This approach avoids the anti-pattern of calling <c>BuildServiceProvider()</c>
/// during configuration, following the <c>IConfigureOptions</c> pattern instead.
/// </para>
/// </remarks>
public sealed class UpcastingOptions
{
	private readonly List<Action<IUpcastingPipeline, IServiceProvider>> _registrationActions = new();

	/// <summary>
	/// Gets or sets whether to enable auto-upcasting during event store replay.
	/// </summary>
	/// <remarks>
	/// When enabled, events loaded from the event store will be automatically upcasted
	/// to their latest version before being applied to aggregates.
	/// </remarks>
	public bool EnableAutoUpcastOnReplay { get; set; }

	/// <summary>
	/// Gets the collection of registration actions to execute when the pipeline is created.
	/// </summary>
	internal IReadOnlyList<Action<IUpcastingPipeline, IServiceProvider>> RegistrationActions => _registrationActions;

	/// <summary>
	/// Adds a registration action to be executed when the pipeline is created.
	/// </summary>
	/// <param name="action">The action that registers upcasters with the pipeline.</param>
	internal void AddRegistration(Action<IUpcastingPipeline, IServiceProvider> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_registrationActions.Add(action);
	}

	/// <summary>
	/// Adds a simple registration action that does not require service provider access.
	/// </summary>
	/// <param name="action">The action that registers upcasters with the pipeline.</param>
	internal void AddRegistration(Action<IUpcastingPipeline> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_registrationActions.Add((pipeline, _) => action(pipeline));
	}
}
