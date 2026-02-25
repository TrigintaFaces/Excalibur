// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing Elasticsearch index templates including creation, updates, and validation.
/// </summary>
public interface IIndexTemplateManager
{
	/// <summary>
	/// Creates or updates an index template in Elasticsearch.
	/// </summary>
	/// <param name="templateName"> The name of the index template. </param>
	/// <param name="template"> The template configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateOrUpdateTemplateAsync(string templateName, IndexTemplateConfiguration template,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an index template from Elasticsearch.
	/// </summary>
	/// <param name="templateName"> The name of the index template to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteTemplateAsync(string templateName, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if an index template exists in Elasticsearch.
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

	/// <summary>
	/// Gets all index templates that match the specified pattern.
	/// </summary>
	/// <param name="namePattern"> The pattern to match template names against. If null, returns all templates. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IEnumerable}" /> containing the matching templates. </returns>
	Task<IEnumerable<IndexTemplateItem>> GetTemplatesAsync(string? namePattern, CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a component template in Elasticsearch.
	/// </summary>
	/// <param name="templateName"> The name of the component template. </param>
	/// <param name="template"> The component template configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateOrUpdateComponentTemplateAsync(string templateName, ComponentTemplateConfiguration template,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a component template from Elasticsearch.
	/// </summary>
	/// <param name="templateName"> The name of the component template to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteComponentTemplateAsync(string templateName, CancellationToken cancellationToken);

	/// <summary>
	/// Migrates an index template to a new version with support for zero-downtime updates.
	/// </summary>
	/// <param name="templateName"> The name of the template to migrate. </param>
	/// <param name="newTemplate"> The new template configuration. </param>
	/// <param name="migrationOptions"> Options controlling the migration process. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TemplateMigrationResult}" /> containing migration results. </returns>
	Task<TemplateMigrationResult> MigrateTemplateAsync(
		string templateName,
		IndexTemplateConfiguration newTemplate,
		TemplateMigrationOptions migrationOptions,
		CancellationToken cancellationToken);
}
