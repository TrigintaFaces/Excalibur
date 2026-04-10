// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using ElasticSearchQuerying.Domain;

using Excalibur.Data.ElasticSearch;

namespace ElasticSearchQuerying.Repositories;

/// <summary>
/// Repository interface for product search operations.
/// Combines CRUD and query capabilities from the Elasticsearch base interfaces.
/// </summary>
public interface IProductRepository : IElasticRepositoryBase<Product>, IElasticRepositoryBaseQuery<Product>;
