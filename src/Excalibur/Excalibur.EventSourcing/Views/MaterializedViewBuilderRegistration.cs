// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Registration record for materialized view builders used for service discovery.
/// </summary>
/// <param name="ViewType">The type of view being built.</param>
/// <param name="BuilderType">The type of the builder implementation.</param>
/// <param name="BuilderInstance">The builder instance.</param>
internal sealed record MaterializedViewBuilderRegistration(
	Type ViewType,
	Type BuilderType,
	object BuilderInstance);
