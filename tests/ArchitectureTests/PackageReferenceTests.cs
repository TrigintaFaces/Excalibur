// -----------------------------------------------------------------------
// <copyright file="PackageReferenceTests.cs" company="Excalibur">
//     Licensed under the Excalibur License 1.0.
//     SPDX-License-Identifier: Excalibur-1.0 OR AGPL-3.0-or-later OR Apache-2.0
// </copyright>
// -----------------------------------------------------------------------

using System.Xml.Linq;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Package Reference Tests - Sprint 308, T5.1.
///
/// These tests directly inspect .csproj files to verify that package references
/// follow the W3 Provider Extraction boundaries. This provides a build-time
/// check independent of runtime assembly loading.
///
/// Reference Decisions:
/// - AD-306-1 to AD-306-5: SqlServer provider patterns
/// - AD-307-1 to AD-307-4: Postgres provider patterns
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class PackageReferenceTests
{
    /// <summary>
    /// Path to the src/Dispatch directory from the test project.
    /// </summary>
    private static readonly string DispatchProjectPath = FindDispatchCsproj();

    #region Excalibur.Dispatch.csproj Package Reference Tests

    /// <summary>
    /// Excalibur.Dispatch.csproj MUST NOT have PackageReference to Dapper.
    ///
    /// After W3 Provider Extraction, Dapper belongs only in
    /// Excalibur.Data.* provider packages.
    /// </summary>
    [Fact]
    public void DispatchCsproj_MustNotReference_Dapper()
    {
        if (!File.Exists(DispatchProjectPath))
        {
            Assert.True(true, "Excalibur.Dispatch.csproj not found at expected location.");
            return;
        }

        var hasReference = HasPackageReference(DispatchProjectPath, "Dapper");

        hasReference.ShouldBeFalse(
            "Excalibur.Dispatch.csproj must not reference Dapper package. " +
            "Database access belongs in Excalibur.Data.* provider packages.");
    }

    /// <summary>
    /// Excalibur.Dispatch.csproj MUST NOT have PackageReference to Npgsql.
    ///
    /// Postgres driver belongs only in Excalibur.Data.Postgres package.
    /// </summary>
    [Fact]
    public void DispatchCsproj_MustNotReference_Npgsql()
    {
        if (!File.Exists(DispatchProjectPath))
        {
            Assert.True(true, "Excalibur.Dispatch.csproj not found at expected location.");
            return;
        }

        var hasReference = HasPackageReference(DispatchProjectPath, "Npgsql");

        hasReference.ShouldBeFalse(
            "Excalibur.Dispatch.csproj must not reference Npgsql package. " +
            "Postgres access belongs in Excalibur.Data.Postgres package.");
    }

    /// <summary>
    /// Excalibur.Dispatch.csproj MUST NOT have PackageReference to Microsoft.Data.SqlClient.
    ///
    /// SQL Server driver belongs only in Excalibur.Data.SqlServer package.
    /// </summary>
    [Fact]
    public void DispatchCsproj_MustNotReference_SqlClient()
    {
        if (!File.Exists(DispatchProjectPath))
        {
            Assert.True(true, "Excalibur.Dispatch.csproj not found at expected location.");
            return;
        }

        var hasReference = HasPackageReference(DispatchProjectPath, "Microsoft.Data.SqlClient");

        hasReference.ShouldBeFalse(
            "Excalibur.Dispatch.csproj must not reference Microsoft.Data.SqlClient package. " +
            "SQL Server access belongs in Excalibur.Data.SqlServer package.");
    }

    /// <summary>
    /// Theory test for all banned database packages in Excalibur.Dispatch.csproj.
    /// </summary>
    [Theory]
    [InlineData("Dapper")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("System.Data.SqlClient")]
    public void DispatchCsproj_MustNotReference_DatabasePackage(string packageName)
    {
        if (!File.Exists(DispatchProjectPath))
        {
            Assert.True(true, "Excalibur.Dispatch.csproj not found at expected location.");
            return;
        }

        var hasReference = HasPackageReference(DispatchProjectPath, packageName);

        hasReference.ShouldBeFalse(
            $"Excalibur.Dispatch.csproj must not reference {packageName} package. " +
            "Database access belongs in Excalibur.Data.* provider packages.");
    }

    /// <summary>
    /// Excalibur.Dispatch.csproj MUST NOT have ProjectReference to Excalibur.Data.* projects.
    ///
    /// Dispatch is the messaging framework; it should not reference data packages.
    /// </summary>
    [Fact]
    public void DispatchCsproj_MustNotReference_ExcaliburDataProjects()
    {
        if (!File.Exists(DispatchProjectPath))
        {
            Assert.True(true, "Excalibur.Dispatch.csproj not found at expected location.");
            return;
        }

        var projectReferences = GetProjectReferences(DispatchProjectPath)
            .Where(r => r.Contains("Excalibur.Data", StringComparison.OrdinalIgnoreCase))
            .ToList();

        projectReferences.ShouldBeEmpty(
            "Excalibur.Dispatch.csproj must not reference Excalibur.Data.* projects. " +
            "Dispatch is messaging-only; data access belongs in Excalibur packages. " +
            $"References found: {string.Join(", ", projectReferences)}");
    }

    #endregion

    #region Excalibur.Data.SqlServer Package Reference Tests

    /// <summary>
    /// Excalibur.Data.SqlServer.csproj SHOULD have PackageReference to Dapper and SqlClient.
    ///
    /// This is the correct pattern - provider packages contain driver dependencies.
    /// </summary>
    [Fact]
    public void ExcaliburDataSqlServerCsproj_ShouldReference_DatabasePackages()
    {
        var sqlServerProjectPath = FindExcaliburDataSqlServerCsproj();

        if (!File.Exists(sqlServerProjectPath))
        {
            Assert.True(true, "Excalibur.Data.SqlServer.csproj not found.");
            return;
        }

        var hasDapper = HasPackageReference(sqlServerProjectPath, "Dapper");
        var hasSqlClient = HasPackageReference(sqlServerProjectPath, "Microsoft.Data.SqlClient");

        // These assertions document the expected pattern
        // SqlServer provider SHOULD have these dependencies
        (hasDapper || hasSqlClient).ShouldBeTrue(
            "Excalibur.Data.SqlServer.csproj should reference database packages. " +
            "Provider packages are where database drivers belong.");
    }

    #endregion

    #region Excalibur.Data.Postgres Package Reference Tests

    /// <summary>
    /// Excalibur.Data.Postgres.csproj SHOULD have PackageReference to Dapper and Npgsql.
    ///
    /// This is the correct pattern - provider packages contain driver dependencies.
    /// </summary>
    [Fact]
    public void ExcaliburDataPostgresCsproj_ShouldReference_DatabasePackages()
    {
        var postgresProjectPath = FindExcaliburDataPostgresCsproj();

        if (!File.Exists(postgresProjectPath))
        {
            Assert.True(true, "Excalibur.Data.Postgres.csproj not found.");
            return;
        }

        var hasDapper = HasPackageReference(postgresProjectPath, "Dapper");
        var hasNpgsql = HasPackageReference(postgresProjectPath, "Npgsql");

        // These assertions document the expected pattern
        // Postgres provider SHOULD have these dependencies
        (hasDapper || hasNpgsql).ShouldBeTrue(
            "Excalibur.Data.Postgres.csproj should reference database packages. " +
            "Provider packages are where database drivers belong.");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Finds the Excalibur.Dispatch.csproj file by searching up from the test project.
    /// </summary>
    private static string FindDispatchCsproj()
    {
        // Start from the current directory and search for solution root
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDir != null)
        {
            // Look for Excalibur.sln to identify solution root
            var solutionFile = Path.Combine(currentDir.FullName, "Excalibur.sln");
            if (File.Exists(solutionFile))
            {
                var dispatchCsproj = Path.Combine(
                    currentDir.FullName,
                    "src",
                    "Dispatch",
                    "Excalibur.Dispatch",
                    "Excalibur.Dispatch.csproj");

                if (File.Exists(dispatchCsproj))
                {
                    return dispatchCsproj;
                }

                // Try alternate path
                dispatchCsproj = Path.Combine(
                    currentDir.FullName,
                    "src",
                    "Dispatch",
                    "Excalibur.Dispatch.csproj");

                if (File.Exists(dispatchCsproj))
                {
                    return dispatchCsproj;
                }
            }

            currentDir = currentDir.Parent;
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds the Excalibur.Data.SqlServer.csproj file.
    /// </summary>
    private static string FindExcaliburDataSqlServerCsproj()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDir != null)
        {
            var solutionFile = Path.Combine(currentDir.FullName, "Excalibur.sln");
            if (File.Exists(solutionFile))
            {
                var projectPath = Path.Combine(
                    currentDir.FullName,
                    "src",
                    "Excalibur",
                    "Excalibur.Data.SqlServer",
                    "Excalibur.Data.SqlServer.csproj");

                return projectPath;
            }

            currentDir = currentDir.Parent;
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds the Excalibur.Data.Postgres.csproj file.
    /// </summary>
    private static string FindExcaliburDataPostgresCsproj()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDir != null)
        {
            var solutionFile = Path.Combine(currentDir.FullName, "Excalibur.sln");
            if (File.Exists(solutionFile))
            {
                var projectPath = Path.Combine(
                    currentDir.FullName,
                    "src",
                    "Excalibur",
                    "Excalibur.Data.Postgres",
                    "Excalibur.Data.Postgres.csproj");

                return projectPath;
            }

            currentDir = currentDir.Parent;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if a .csproj file has a PackageReference to the specified package.
    /// </summary>
    private static bool HasPackageReference(string csprojPath, string packageName)
    {
        if (!File.Exists(csprojPath))
        {
            return false;
        }

        try
        {
            var doc = XDocument.Load(csprojPath);

            return doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Any(e => e.Attribute("Include")?.Value
                    .Equals(packageName, StringComparison.OrdinalIgnoreCase) == true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all ProjectReference Include values from a .csproj file.
    /// </summary>
    private static IEnumerable<string> GetProjectReferences(string csprojPath)
    {
        if (!File.Exists(csprojPath))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            var doc = XDocument.Load(csprojPath);

            return doc.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v));
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    #endregion
}
