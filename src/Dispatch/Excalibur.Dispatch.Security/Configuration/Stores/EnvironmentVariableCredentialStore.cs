// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Retrieves credentials from environment variables. Environment variables are a secure way to provide credentials in containerized environments.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="EnvironmentVariableCredentialStore" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
/// <param name="prefix"> Optional prefix for environment variable names. </param>
public sealed partial class EnvironmentVariableCredentialStore(
	ILogger<EnvironmentVariableCredentialStore> logger,
	string prefix = "DISPATCH_") : ICredentialStore
{
	private readonly ILogger<EnvironmentVariableCredentialStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly string _prefix = prefix ?? string.Empty;

	/// <summary>
	/// Retrieves a credential from environment variables.
	/// </summary>
	/// <param name="key"> The credential key. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The secure credential or null if not found. </returns>
	public Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		// Convert key to environment variable format (uppercase with underscores)
		var envVarName = _prefix + key.Replace('.', '_').Replace(':', '_').ToUpperInvariant();

		var value = Environment.GetEnvironmentVariable(envVarName);
		if (string.IsNullOrEmpty(value))
		{
			// Also try without prefix
			value = Environment.GetEnvironmentVariable(key.Replace('.', '_').Replace(':', '_').ToUpperInvariant());
		}

		if (string.IsNullOrEmpty(value))
		{
			LogCredentialNotFound(key);
			return Task.FromResult<SecureString?>(null);
		}

		LogCredentialRetrieved(key, envVarName);

		var secureString = new SecureString();
		foreach (var c in value)
		{
			secureString.AppendChar(c);
		}

		secureString.MakeReadOnly();

		// Clear the original string from memory
		GC.Collect();

		return Task.FromResult<SecureString?>(secureString);
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.EnvironmentVariableNotFound, LogLevel.Debug, "Credential {Key} not found in environment variables")]
	private partial void LogCredentialNotFound(string key);

	[LoggerMessage(SecurityEventId.EnvironmentVariableFound, LogLevel.Debug, "Credential {Key} retrieved from environment variable {EnvVar}")]
	private partial void LogCredentialRetrieved(string key, string envVar);
}
