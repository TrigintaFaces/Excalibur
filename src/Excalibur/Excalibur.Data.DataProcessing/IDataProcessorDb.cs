// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents the database interface specifically for data processor-related operations.
/// </summary>
/// <remarks> This interface extends <see cref="IDb" /> to provide database functionality tailored to data processing requirements. </remarks>
public interface IDataProcessorDb : IDb;
