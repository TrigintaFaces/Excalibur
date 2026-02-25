// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.MaterializedViews;
using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.Data.Tests.ElasticSearch.MaterializedViews;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticSearchMaterializedViewExtensionsShould
{
	[Fact]
	public void ThrowWhenBuilderIsNullForConfigureOverload()
	{
		IMaterializedViewsBuilder builder = null!;
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticSearch(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var builder = A.Fake<IMaterializedViewsBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticSearch((Action<ElasticSearchMaterializedViewStoreOptions>)null!));
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForUriOverload()
	{
		IMaterializedViewsBuilder builder = null!;
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticSearch("http://localhost:9200"));
	}

	[Fact]
	public void ThrowWhenNodeUriIsNull()
	{
		var builder = A.Fake<IMaterializedViewsBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentException>(
			() => builder.UseElasticSearch((string)null!));
	}

	[Fact]
	public void ThrowWhenNodeUriIsEmpty()
	{
		var builder = A.Fake<IMaterializedViewsBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentException>(
			() => builder.UseElasticSearch(""));
	}

	[Fact]
	public void ThrowWhenNodeUriIsWhitespace()
	{
		var builder = A.Fake<IMaterializedViewsBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentException>(
			() => builder.UseElasticSearch("   "));
	}
}
