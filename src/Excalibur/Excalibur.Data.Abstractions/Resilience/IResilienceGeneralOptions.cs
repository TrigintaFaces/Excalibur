// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// General resilience configuration options including timeout and feature enablement.
/// </summary>
/// <remarks>
/// <para>
/// Validation of these options should be performed via <c>IValidateOptions&lt;T&gt;</c>
/// implementations registered with <c>ValidateOnStart</c>, rather than through an
/// imperative <c>Validate()</c> method on the options interface itself.
/// </para>
/// </remarks>
public interface IResilienceGeneralOptions
{
	/// <summary>
	/// Gets or sets the default timeout for DataRequest execution.
	/// </summary>
	/// <value>
	/// The default timeout for DataRequest execution.
	/// </value>
	TimeSpan DefaultTimeout { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable resilience policies.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable resilience policies.
	/// </value>
	bool EnableResilience { get; set; }

	/// <summary>
	/// Gets provider-specific resilience options.
	/// </summary>
	/// <value>
	/// Provider-specific resilience options.
	/// </value>
	IDictionary<string, object> ProviderSpecificOptions { get; }
}
