// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Monitors persistence options for runtime configuration changes.
/// </summary>
/// <typeparam name="TOptions"> The type of persistence options. </typeparam>
public interface IPersistenceOptionsMonitor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> : IOptionsMonitor<TOptions>
	where TOptions : class, IPersistenceOptions
{
	/// <summary>
	/// Gets options for a specific provider.
	/// </summary>
	/// <param name="providerName"> The name of the provider. </param>
	/// <returns> The provider options. </returns>
	TOptions GetProviderOptions(string providerName);

	/// <summary>
	/// Registers a change callback for a specific provider.
	/// </summary>
	/// <param name="providerName"> The name of the provider. </param>
	/// <param name="listener"> The change listener. </param>
	/// <returns> A disposable registration. </returns>
	IDisposable OnProviderChange(string providerName, Action<TOptions, string> listener);

	/// <summary>
	/// Validates options before they are applied.
	/// </summary>
	/// <param name="options"> The options to validate. </param>
	/// <returns> Validation results. </returns>
	IEnumerable<string> ValidateOptions(TOptions options);

	/// <summary>
	/// Forces a reload of options from the configuration source.
	/// </summary>
	/// <param name="providerName"> Optional provider name to reload; if null, reloads all. </param>
	void ForceReload(string? providerName = null);

	/// <summary>
	/// Gets the last time options were changed.
	/// </summary>
	/// <param name="providerName"> The name of the provider. </param>
	/// <returns> The last change time, or null if never changed. </returns>
	DateTimeOffset? GetLastChangeTime(string providerName);
}
