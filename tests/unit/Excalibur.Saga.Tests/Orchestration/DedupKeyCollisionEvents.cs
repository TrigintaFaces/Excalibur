// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

// Two event types with the SAME simple name in sibling namespaces. This is the whole fixture for
// SagaEventDedupKeyIsNamespaceQualifiedShould: it is legal C#, it is ordinary in a codebase with a module
// per bounded context, and it is exactly what a simple-name-keyed dedup derivation collapses onto one key.
//
// They live in this file rather than beside the arms because a file cannot mix file-scoped and block
// namespace declarations (CS8955), and the arms need a file-scoped namespace to match the rest of the suite.
namespace Excalibur.Saga.Tests.Orchestration.DedupKeyCollision.Alpha
{
	/// <summary>An order-placed event belonging to one bounded context.</summary>
	internal sealed class OrderPlaced : ISagaEvent
	{
		public required string SagaId { get; init; }

		public string? StepId { get; init; }
	}
}

namespace Excalibur.Saga.Tests.Orchestration.DedupKeyCollision.Beta
{
	/// <summary>A DIFFERENT order-placed event, in a different bounded context, same simple name.</summary>
	internal sealed class OrderPlaced : ISagaEvent
	{
		public required string SagaId { get; init; }

		public string? StepId { get; init; }
	}
}
