// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Factory for creating and selecting serverless host providers.
/// </summary>
public interface IServerlessHostProviderFactory
{
	/// <summary>
	/// Gets all available providers.
	/// </summary>
	/// <value>All available providers.</value>
	IEnumerable<IServerlessHostProvider> AvailableProviders { get; }

	/// <summary>
	/// Creates the appropriate provider for the current environment.
	/// </summary>
	/// <param name="preferredPlatform"> The preferred platform, if any. </param>
	/// <returns> The selected provider. </returns>
	IServerlessHostProvider CreateProvider(ServerlessPlatform? preferredPlatform = null);

	/// <summary>
	/// Gets a provider for a specific platform.
	/// </summary>
	/// <param name="platform"> The target platform. </param>
	/// <returns> The provider for the specified platform. </returns>
	IServerlessHostProvider GetProvider(ServerlessPlatform platform);

	/// <summary>
	/// Detects the current serverless platform based on environment variables.
	/// </summary>
	/// <returns> The detected platform. </returns>
	ServerlessPlatform DetectPlatform();

	/// <summary>
	/// Registers a custom provider.
	/// </summary>
	/// <param name="provider"> The provider to register. </param>
	void RegisterProvider(IServerlessHostProvider provider);
}
