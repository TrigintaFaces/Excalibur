// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Represents the result of a migration or rollback operation.
/// </summary>
/// <remarks>
/// This record captures the outcome of migration operations including:
/// <list type="bullet">
/// <item>Whether the operation succeeded</item>
/// <item>Which migrations were applied or removed</item>
/// <item>Any error that occurred during the operation</item>
/// </list>
/// </remarks>
public sealed record MigrationResult
{
	/// <summary>
	/// Gets a value indicating whether the migration operation succeeded.
	/// </summary>
	/// <value><see langword="true"/> if the operation completed successfully; otherwise, <see langword="false"/>.</value>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the migrations that were applied or removed during this operation.
	/// </summary>
	/// <value>A list of migrations in the order they were processed. Empty if no migrations were processed.</value>
	public IReadOnlyList<AppliedMigration> AppliedMigrations { get; init; } = [];

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	/// <value>The error message, or <see langword="null"/> if the operation succeeded.</value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the exception that caused the failure, if any.
	/// </summary>
	/// <value>The exception, or <see langword="null"/> if the operation succeeded or no exception was thrown.</value>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Creates a successful migration result.
	/// </summary>
	/// <param name="appliedMigrations">The migrations that were applied.</param>
	/// <returns>A successful migration result.</returns>
	public static MigrationResult Succeeded(IReadOnlyList<AppliedMigration> appliedMigrations) =>
		new()
		{
			Success = true,
			AppliedMigrations = appliedMigrations
		};

	/// <summary>
	/// Creates a successful migration result with no migrations applied.
	/// </summary>
	/// <returns>A successful migration result indicating no migrations were needed.</returns>
	public static MigrationResult NoMigrationsPending() =>
		new()
		{
			Success = true,
			AppliedMigrations = []
		};

	/// <summary>
	/// Creates a failed migration result.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="exception">The exception that caused the failure, if any.</param>
	/// <returns>A failed migration result.</returns>
	public static MigrationResult Failed(string errorMessage, Exception? exception = null) =>
		new()
		{
			Success = false,
			ErrorMessage = errorMessage,
			Exception = exception
		};
}
