// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Extension methods for <see cref="IIndexTemplateManager"/>.
/// </summary>
public static class IndexTemplateManagerExtensions
{
	/// <summary>Creates or updates a component template in OpenSearch.</summary>
	public static Task<bool> CreateOrUpdateComponentTemplateAsync(
		this IIndexTemplateManager manager, string templateName, ComponentTemplateConfiguration template, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(manager);
		return ((IndexTemplateManager)manager).CreateOrUpdateComponentTemplateAsync(templateName, template, cancellationToken);
	}

	/// <summary>Deletes a component template from OpenSearch.</summary>
	public static Task<bool> DeleteComponentTemplateAsync(
		this IIndexTemplateManager manager, string templateName, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(manager);
		return ((IndexTemplateManager)manager).DeleteComponentTemplateAsync(templateName, cancellationToken);
	}

	/// <summary>Migrates an index template to a new version with zero-downtime support.</summary>
	public static Task<TemplateMigrationResult> MigrateTemplateAsync(
		this IIndexTemplateManager manager, string templateName, IndexTemplateConfiguration newTemplate,
		TemplateMigrationOptions migrationOptions, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(manager);
		return ((IndexTemplateManager)manager).MigrateTemplateAsync(templateName, newTemplate, migrationOptions, cancellationToken);
	}
}
