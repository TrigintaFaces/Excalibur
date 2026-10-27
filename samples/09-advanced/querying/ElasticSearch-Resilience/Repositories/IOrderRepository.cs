// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch;
using ElasticSearch_Resilience.Domain;

namespace ElasticSearch_Resilience.Repositories;

public interface IOrderRepository : IElasticRepositoryBase<Order>, IElasticRepositoryBaseQuery<Order>
{
}
