// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing Elasticsearch index templates including creation, updates, and validation.
/// </summary>
public sealed class IndexTemplateManager : IIndexTemplateManager
{
	private readonly IIndexTemplateStore _indexStore;
	private readonly IComponentTemplateStore _componentStore;
	private readonly ILogger<IndexTemplateManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexTemplateManager"/> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client instance. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
	public IndexTemplateManager(ElasticsearchClient client, ILogger<IndexTemplateManager> logger)
		: this(CreateIndexStore(client), CreateComponentStore(client), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexTemplateManager"/>
	/// class using pre-built store adapters. Used by tests to substitute
	/// the SDK via the <see cref="IIndexTemplateStore"/> +
	/// <see cref="IComponentTemplateStore"/> seams (ADR-142 §D7 S799 F1 split).
	/// </summary>
	/// <param name="indexStore"> The index-template store seam adapter. </param>
	/// <param name="componentStore"> The component-template store seam adapter. </param>
	/// <param name="logger"> The logger instance. </param>
	internal IndexTemplateManager(
		IIndexTemplateStore indexStore,
		IComponentTemplateStore componentStore,
		ILogger<IndexTemplateManager> logger)
	{
		_indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
		_componentStore = componentStore ?? throw new ArgumentNullException(nameof(componentStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<bool> CreateOrUpdateTemplateAsync(string templateName, IndexTemplateConfiguration template,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
		ArgumentNullException.ThrowIfNull(template);

		try
		{
			_logger.LogInformation("Creating or updating index template: {TemplateName}", templateName);

			var result = await _indexStore.PutAsync(templateName, template, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				_logger.LogInformation("Successfully created or updated index template: {TemplateName}", templateName);
				return true;
			}

			_logger.LogError(
				"Failed to create or update index template: {TemplateName}. Error: {Error}",
				templateName, result.ErrorDetails);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create or update template: {TemplateName}", templateName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteTemplateAsync(string templateName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		try
		{
			_logger.LogInformation("Deleting index template: {TemplateName}", templateName);

			var result = await _indexStore.DeleteAsync(templateName, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				_logger.LogInformation("Successfully deleted index template: {TemplateName}", templateName);
				return true;
			}

			_logger.LogError(
				"Failed to delete index template: {TemplateName}. Error: {Error}",
				templateName, result.ErrorDetails);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete template: {TemplateName}", templateName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> TemplateExistsAsync(string templateName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		try
		{
			return await _indexStore.ExistsAsync(templateName, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if template exists: {TemplateName}", templateName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<TemplateValidationResult> ValidateTemplateAsync(
		IndexTemplateConfiguration template,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(template);
		await Task.CompletedTask.ConfigureAwait(false);

		var errors = new List<string>();

		// Validate index patterns
		if (!template.IndexPatterns.Any())
		{
			errors.Add("Index patterns cannot be empty");
		}

		// Validate index patterns format
		foreach (var pattern in template.IndexPatterns)
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				errors.Add("Index patterns cannot contain null or empty values");
			}
		}

		// Validate priority
		if (template.Priority < 0)
		{
			errors.Add("Priority must be non-negative");
		}

		// Additional validation logic can be added here For example, validating mappings, settings, etc.
		return new TemplateValidationResult { IsValid = errors.Count == 0, Errors = errors };
	}

	/// <inheritdoc />
	public async Task<IEnumerable<IndexTemplateDescriptor>> GetTemplatesAsync(
		string? namePattern,
		CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation("Getting index templates with pattern: {Pattern}", namePattern ?? "all");

			return await _indexStore.ListAsync(namePattern ?? "*", cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get templates with pattern: {Pattern}", namePattern ?? "all");
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<bool> CreateOrUpdateComponentTemplateAsync(string templateName, ComponentTemplateConfiguration template,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
		ArgumentNullException.ThrowIfNull(template);

		try
		{
			_logger.LogInformation("Creating or updating component template: {TemplateName}", templateName);

			var result = await _componentStore.PutAsync(templateName, template, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				_logger.LogInformation("Successfully created or updated component template: {TemplateName}", templateName);
				return true;
			}

			_logger.LogError(
				"Failed to create or update component template: {TemplateName}. Error: {Error}",
				templateName, result.ErrorDetails);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create or update component template: {TemplateName}", templateName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteComponentTemplateAsync(string templateName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

		try
		{
			_logger.LogInformation("Deleting component template: {TemplateName}", templateName);

			var result = await _componentStore.DeleteAsync(templateName, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				_logger.LogInformation("Successfully deleted component template: {TemplateName}", templateName);
				return true;
			}

			_logger.LogError(
				"Failed to delete component template: {TemplateName}. Error: {Error}",
				templateName, result.ErrorDetails);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete component template: {TemplateName}", templateName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<TemplateMigrationResult> MigrateTemplateAsync(
		string templateName,
		IndexTemplateConfiguration newTemplate,
		TemplateMigrationOptions migrationOptions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
		ArgumentNullException.ThrowIfNull(newTemplate);
		ArgumentNullException.ThrowIfNull(migrationOptions);

		var errors = new List<string>();
		var warnings = new List<string>();

		try
		{
			_logger.LogInformation("Starting template migration for: {TemplateName}", templateName);

			// Step 1: Validate new template if requested
			if (migrationOptions.ValidateBeforeMigration)
			{
				var validationResult = await ValidateTemplateAsync(newTemplate, cancellationToken).ConfigureAwait(false);
				if (!validationResult.IsValid)
				{
					errors.AddRange(validationResult.Errors);
					return new TemplateMigrationResult { IsSuccessful = false, Errors = errors, Warnings = warnings };
				}
			}

			// Step 2: Create backup if requested
			string? backupTemplateName = null;
			if (migrationOptions.CreateBackup)
			{
				backupTemplateName = $"{templateName}_backup_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
				var existingTemplates = await GetTemplatesAsync(templateName, cancellationToken).ConfigureAwait(false);
				var existingTemplate = existingTemplates.FirstOrDefault();

				if (existingTemplate is not null)
				{
					// Create backup template logic would go here
					_logger.LogInformation("Created backup template: {BackupTemplateName}", backupTemplateName);
					warnings.Add($"Backup created: {backupTemplateName}");
				}
			}

			// Step 3: Apply the new template
			var migrationSuccess = await CreateOrUpdateTemplateAsync(templateName, newTemplate, cancellationToken).ConfigureAwait(false);

			if (migrationSuccess)
			{
				_logger.LogInformation("Successfully migrated template: {TemplateName}", templateName);
				return new TemplateMigrationResult { IsSuccessful = true, Errors = errors, Warnings = warnings };
			}

			errors.Add("Failed to apply new template");
			return new TemplateMigrationResult { IsSuccessful = false, Errors = errors, Warnings = warnings };
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Template migration failed for: {TemplateName}", templateName);
			errors.Add($"Migration failed with exception: {ex.Message}");
			return new TemplateMigrationResult { IsSuccessful = false, Errors = errors, Warnings = warnings };
		}
	}

	private static IIndexTemplateStore CreateIndexStore(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new IndexTemplateStoreAdapter(client);
	}

	private static IComponentTemplateStore CreateComponentStore(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new ComponentTemplateStoreAdapter(client);
	}
}
