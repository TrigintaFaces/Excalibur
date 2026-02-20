// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Represents a plan for migrating events between streams.
/// </summary>
/// <param name="SourceStream">The source stream identifier to read events from.</param>
/// <param name="TargetStream">The target stream identifier to write events to.</param>
/// <param name="EventFilter">
/// Optional predicate to filter which events are migrated.
/// When <see langword="null"/>, all events are included.
/// </param>
/// <param name="TransformFunc">
/// Optional function to transform events during migration.
/// When <see langword="null"/>, events are copied as-is.
/// </param>
public sealed record MigrationPlan(
	string SourceStream,
	string TargetStream,
	Func<StoredEvent, bool>? EventFilter = null,
	Func<StoredEvent, StoredEvent>? TransformFunc = null);
