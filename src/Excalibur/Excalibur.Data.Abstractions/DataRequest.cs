// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// A specialized base class for database requests using an <see cref="IDbConnection" /> and a specific return model.
/// </summary>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public abstract class DataRequest<TModel> : DataRequestBase<IDbConnection, TModel>;
