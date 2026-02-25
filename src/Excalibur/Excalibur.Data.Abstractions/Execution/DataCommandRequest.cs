// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions.Execution;

/// <summary>
/// Provider-neutral description of a data command to execute against a data store. Avoids any dependency on Dapper or provider SDKs.
/// </summary>
public sealed class DataCommandRequest
{
	/// <inheritdoc/>
	public DataCommandRequest(
		string commandText,
		IReadOnlyDictionary<string, object?>? parameters = null,
		CommandType? commandType = null,
		int? commandTimeoutSeconds = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
		CommandText = commandText;
		Parameters = parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
		CommandType = commandType;
		CommandTimeoutSeconds = commandTimeoutSeconds;
	}

	/// <summary>
	/// Gets the command text (e.g., SQL) to execute.
	/// </summary>
	/// <value>The current <see cref="CommandText"/> value.</value>
	public string CommandText { get; }

	/// <summary>
	/// Gets optional named parameters for the command.
	/// </summary>
	/// <value>The current <see cref="Parameters"/> value.</value>
	public IReadOnlyDictionary<string, object?> Parameters { get; }

	/// <summary>
	/// Gets optional command type; defaults to CommandType.Text.
	/// </summary>
	/// <value>The current <see cref="CommandType"/> value.</value>
	public CommandType? CommandType { get; }

	/// <summary>
	/// Gets optional command timeout in seconds.
	/// </summary>
	/// <value>The current <see cref="CommandTimeoutSeconds"/> value.</value>
	public int? CommandTimeoutSeconds { get; }
}
