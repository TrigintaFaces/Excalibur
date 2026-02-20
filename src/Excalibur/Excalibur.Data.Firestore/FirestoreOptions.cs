// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Configuration options for Google Cloud Firestore.
/// </summary>
public sealed class FirestoreOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	[Required]
	public string Name { get; set; } = "Firestore";

	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the default collection name for operations.
	/// </summary>
	public string? DefaultCollection { get; set; }

	/// <summary>
	/// Gets or sets the path to the service account JSON credentials file.
	/// If not specified, uses application default credentials.
	/// </summary>
	public string? CredentialsPath { get; set; }

	/// <summary>
	/// Gets or sets the JSON content of the service account credentials.
	/// Alternative to <see cref="CredentialsPath"/> for environments like containers.
	/// </summary>
	public string? CredentialsJson { get; set; }

	/// <summary>
	/// Gets or sets the Firestore emulator host for local development.
	/// Example: "localhost:8080".
	/// </summary>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets the timeout for operations in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are not set.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProjectId) && string.IsNullOrWhiteSpace(EmulatorHost))
		{
			throw new InvalidOperationException(
				"Either ProjectId or EmulatorHost must be specified for Firestore configuration.");
		}
	}
}
