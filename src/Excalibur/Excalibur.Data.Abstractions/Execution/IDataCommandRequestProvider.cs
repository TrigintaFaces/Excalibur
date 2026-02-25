// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Execution;

/// <summary>
/// Provides a provider-neutral <see cref="DataCommandRequest"/> for execution against a data store.
/// Implementations must not depend on Dapper or provider SDK types.
/// </summary>
public interface IDataCommandRequestProvider
{
	/// <inheritdoc/>
	DataCommandRequest CommandRequest { get; }
}
