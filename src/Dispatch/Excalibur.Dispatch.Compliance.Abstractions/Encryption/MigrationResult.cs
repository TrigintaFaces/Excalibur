// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of a single encryption migration operation.
/// </summary>
public sealed record EncryptionMigrationResult
{
	/// <summary>
	/// Gets a value indicating whether the migration succeeded.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the migrated encrypted data if successful.
	/// </summary>
	public EncryptedData? MigratedData { get; init; }

	/// <summary>
	/// Gets the error message if the migration failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the exception that caused the failure, if any.
	/// </summary>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets the duration of the migration operation.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets the source key identifier from which data was migrated.
	/// </summary>
	public string? SourceKeyId { get; init; }

	/// <summary>
	/// Gets the target key identifier to which data was migrated.
	/// </summary>
	public string? TargetKeyId { get; init; }

	/// <summary>
	/// Gets the source algorithm from which data was migrated.
	/// </summary>
	public EncryptionAlgorithm? SourceAlgorithm { get; init; }

	/// <summary>
	/// Gets the target algorithm to which data was migrated.
	/// </summary>
	public EncryptionAlgorithm? TargetAlgorithm { get; init; }

	/// <summary>
	/// Creates a successful migration result.
	/// </summary>
	/// <param name="migratedData"> The migrated encrypted data. </param>
	/// <param name="duration"> The duration of the operation. </param>
	/// <param name="sourceKeyId"> The source key identifier. </param>
	/// <param name="targetKeyId"> The target key identifier. </param>
	/// <returns> A successful migration result. </returns>
	public static EncryptionMigrationResult Succeeded(
		EncryptedData migratedData,
		TimeSpan duration,
		string sourceKeyId,
		string targetKeyId) =>
		new()
		{
			Success = true,
			MigratedData = migratedData,
			Duration = duration,
			SourceKeyId = sourceKeyId,
			TargetKeyId = targetKeyId,
			SourceAlgorithm = migratedData.Algorithm,
			TargetAlgorithm = migratedData.Algorithm,
		};

	/// <summary>
	/// Creates a failed migration result.
	/// </summary>
	/// <param name="errorMessage"> The error message describing the failure. </param>
	/// <param name="exception"> The exception that caused the failure. </param>
	/// <param name="duration"> The duration before the failure. </param>
	/// <returns> A failed migration result. </returns>
	public static EncryptionMigrationResult Failed(
		string errorMessage,
		Exception? exception = null,
		TimeSpan duration = default) =>
		new()
		{
			Success = false,
			ErrorMessage = errorMessage,
			Exception = exception,
			Duration = duration,
		};
}
