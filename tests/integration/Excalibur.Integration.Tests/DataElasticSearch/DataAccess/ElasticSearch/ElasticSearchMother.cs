// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Integration.Tests.DataElasticSearch.Helpers;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch;

internal static class ElasticSearchMother
{
	public static TestElasticDocument CreateTestDocument(string? name = null)
	{
		return new TestElasticDocument
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = name ?? $"doc-{Guid.NewGuid():N}",
		};
	}

	public static IEnumerable<TestElasticDocument> CreateManyTestDocuments(int count)
	{
		for (var i = 0; i < count; i++)
		{
			yield return CreateTestDocument();
		}
	}
}
