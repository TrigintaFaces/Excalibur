// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Defines the branching strategy for conditional steps.
/// </summary>
public enum BranchingStrategy
{
	/// <summary>
	/// Simple if-then-else branching.
	/// </summary>
	Simple = 0,

	/// <summary>
	/// Multi-way branching with multiple conditions.
	/// </summary>
	MultiWay = 1,

	/// <summary>
	/// Branching based on pattern matching.
	/// </summary>
	PatternMatching = 2,

	/// <summary>
	/// Branching based on state machine transitions.
	/// </summary>
	StateMachine = 3,
}

