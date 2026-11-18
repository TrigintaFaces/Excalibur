// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Microsoft.VisualBasic.FileIO;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

internal static class BenchmarkCsvReports
{
    internal static bool IsPositiveFiniteDuration(string mean)
    {
        var parts = mean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && parts[1] is "ns" or "us" or "μs" or "µs" or "ms" or "s"
            && double.TryParse(parts[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            && value > 0;
    }

    // The parser owns and disposes the supplied reader; results are fully materialized.
    internal static string[] ReadMeans(TextReader reader)
    {
        using var parser = new TextFieldParser(reader)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false,
        };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("Missing benchmark CSV header.");
        if (header.Count(field => field == "Mean") != 1)
        {
            throw new InvalidDataException("Expected exactly one Mean column.");
        }

        var meanIndex = Array.IndexOf(header, "Mean");
        var means = new List<string>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row is null || row.Length != header.Length || string.IsNullOrWhiteSpace(row[meanIndex]))
            {
                throw new InvalidDataException("Incomplete benchmark CSV row.");
            }

            means.Add(row[meanIndex]);
        }

        if (means.Count == 0)
        {
            throw new InvalidDataException("Benchmark CSV contains no results.");
        }

        return means.ToArray();
    }
}
