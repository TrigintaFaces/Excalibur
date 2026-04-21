// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing OpenSearch index templates including creation, updates, and validation.
/// </summary>
public interface IIndexTemplateManager
{
	/// <summary>
	/// Creates or updates an index template in OpenSearch.
	/// </summary>
	/// <param name="templateName"> The name of the index template. </param>
	/// <param name="template"> The template configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateOrUpdateTemplateAsync(string templateName, IndexTemplateConfiguration template,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an index template from OpenSearch.
	/// </summary>
	/// <param name="templateName"> The name of the index template to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteTemplateAsync(string templateName, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if an index template exists in OpenSearch.
	/// </summary>
	/// <param name="templateName"> The name of the index template to check. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the template exists. </returns>
	Task<bool> TemplateExistsAsync(string templateName, CancellationToken cancellationToken);

	/// <summary>
	/// Validates an index template configuration without applying it.
	/// </summary>
	/// <param name="template"> The template configuration to validate. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TemplateValidationResult}" /> containing validation results. </returns>
	Task<TemplateValidationResult>
		ValidateTemplateAsync(IndexTemplateConfiguration template, CancellationToken cancellationToken);
}
