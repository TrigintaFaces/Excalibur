// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents a database interface for data processing operations.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IDb" />, providing access to the underlying database connection for use in data processing workflows.
/// </remarks>
public interface IDataToProcessDb : IDb;
