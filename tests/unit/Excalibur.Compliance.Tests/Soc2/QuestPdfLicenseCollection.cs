// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using QuestPDF.Infrastructure;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Selects a QuestPDF license once for every test that exports a PDF, standing in for the hosting
/// application. The framework deliberately never assigns <c>QuestPDF.Settings.License</c>, so a PDF
/// export throws unless the consumer has selected one -- and in this assembly the consumer is the test
/// host.
/// </summary>
public sealed class QuestPdfLicenseFixture
{
	/// <summary>Initializes a new instance of the <see cref="QuestPdfLicenseFixture"/> class.</summary>
	public QuestPdfLicenseFixture() => QuestPDF.Settings.License = LicenseType.Community;
}

/// <summary>
/// Runs every PDF-exporting test class in one collection.
/// </summary>
/// <remarks>
/// <c>QuestPDF.Settings.License</c> is process-global, and one of these tests deliberately changes it to
/// prove the exporter leaves a host's selection alone. Sharing a collection makes those classes run
/// sequentially, so that mutation cannot race a sibling mid-export.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class QuestPdfLicenseCollection : ICollectionFixture<QuestPdfLicenseFixture>
{
	/// <summary>The collection name.</summary>
	public const string Name = "QuestPdfLicense";
}
