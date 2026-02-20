// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Represents the base interface for all policy types.
/// </summary>
/// <remarks>
/// This interface acts as a marker for policies, allowing type constraints and polymorphism for policy-related operations. Derived
/// interfaces or classes should define specific properties and methods for the policy they represent.
/// </remarks>
public interface IPolicy;
