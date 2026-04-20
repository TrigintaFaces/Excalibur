// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using ElasticSearch_Paging.Domain;

namespace ElasticSearch_Paging.Repositories;

public interface ILogRepository : IElasticRepositoryBase<LogEntry>, IElasticRepositoryBaseQuery<LogEntry>
{
}
