// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Framework-owned descriptor for an Elasticsearch index template returned by
/// <see cref="IIndexTemplateManager.GetTemplatesAsync"/>. Mirrors the subset of
/// data consumers actually need from the Elastic SDK
/// <c>IndexTemplateItem</c> without leaking the SDK type across the framework
/// public boundary (S802-B, COMPASS msg 1931).
/// </summary>
/// <remarks>
/// This descriptor intentionally does not expose the template's mappings or
/// settings. Those are SDK-typed shapes (<c>TypeMapping</c>,
/// <c>IndexSettings</c>) and are carried on <see cref="IndexTemplateConfiguration"/>
/// on the write path. If a future consumer needs to inspect the mapping/settings
/// payload on read, prefer adding opaque JSON strings here (serialized via the
/// SDK) rather than re-exposing SDK types.
/// </remarks>
public sealed record IndexTemplateDescriptor
{
	/// <summary>
	/// Gets the template name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the index patterns the template matches. Never <see langword="null"/>;
	/// defaults to an empty list when the source template declares no patterns.
	/// </summary>
	public required IReadOnlyList<string> IndexPatterns { get; init; }

	/// <summary>
	/// Gets the template priority, if set. Higher priority wins when multiple
	/// templates match the same index name.
	/// </summary>
	public int? Priority { get; init; }

	/// <summary>
	/// Gets the template version, if set. Opaque user-supplied value — the
	/// store does not interpret it.
	/// </summary>
	public long? Version { get; init; }

	/// <summary>
	/// Gets the names of component templates composed into this template, if any.
	/// </summary>
	public IReadOnlyList<string>? ComposedOf { get; init; }

	/// <summary>
	/// Gets user-supplied metadata attached to the template, if any. Values are
	/// projected to their string representation so the framework does not leak
	/// Elastic SDK <c>object?</c> value typing across its public boundary.
	/// </summary>
	/// <remarks>
	/// Elasticsearch template <c>_meta</c> is opaque user data; the SDK surfaces
	/// values as <c>object?</c>. Consumers that need the raw JSON shape should
	/// query the cluster directly via their own Elastic client. For typical
	/// inspection scenarios (name, tags, labels) the string projection is
	/// sufficient and keeps the framework's descriptor SDK-free (bd-cpsn3h, S811).
	/// </remarks>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
