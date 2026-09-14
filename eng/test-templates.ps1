#!/usr/bin/env pwsh
<#
.SYNOPSIS
    CI smoke tests for Excalibur dotnet new templates.
    Validates template installation, instantiation with all option combinations,
    and project structure correctness.

.DESCRIPTION
    Covers the template acceptance criteria:
    - CI step installs templates from local source (dotnet new install)
    - CI step runs dotnet new for each template with default options and verifies structure
    - CI step runs dotnet new dispatch-api with each --transport option
    - CI step runs dotnet new excalibur-ddd with each --database option
    - CI step runs dotnet new with --include-tests and verifies test project exists
    - Template pack step (dotnet pack) produces Excalibur.Dispatch.Templates.nupkg
    - CI fails if any template produces a project that does not compile (structure validation)
    - CI runs on PRs that touch templates/ or src/ directories

.PARAMETER CleanUp
    Remove generated test projects after validation. Default: true.
#>

param(
    [switch]$NoCleanUp
)

$ErrorActionPreference = 'Stop'
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0
$script:Failures = @()

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$RepoRoot/templates")) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}
if (-not (Test-Path "$RepoRoot/templates")) {
    $RepoRoot = $PSScriptRoot | Split-Path -Parent
}
# Fallback: use the script's location to find repo root
if (-not (Test-Path "$RepoRoot/templates")) {
    $RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
    if (-not (Test-Path "$RepoRoot/templates")) {
        Write-Error "Cannot find templates directory. Run from repo root or eng/ directory."
        exit 1
    }
}

$TemplatesDir = Join-Path $RepoRoot "templates"
$TestOutputDir = Join-Path $RepoRoot "artifacts" "template-tests"

# Template definitions with their options
$Templates = @(
    @{
        ShortName    = "dispatch-api"
        SourceName   = "Company.DispatchApi"
        TemplatePath = Join-Path $TemplatesDir "dispatch-api"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs")
        Databases    = @()
        HasDocker    = $true
        HasTests     = $true
        ExpectedFiles = @("Program.cs", "appsettings.json", "Controllers/OrdersController.cs", "Actions/CreateOrderAction.cs", "Actions/GetOrderAction.cs", "Handlers/CreateOrderHandler.cs", "Handlers/GetOrderHandler.cs")
    },
    @{
        ShortName    = "dispatch-worker"
        SourceName   = "Company.DispatchWorker"
        TemplatePath = Join-Path $TemplatesDir "dispatch-worker"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs")
        Databases    = @()
        HasDocker    = $true
        HasTests     = $true
        ExpectedFiles = @("Program.cs", "appsettings.json", "Handlers/OrderCreatedEventHandler.cs", "Workers/OrderProcessingWorker.cs")
    },
    @{
        ShortName    = "excalibur-ddd"
        SourceName   = "Company.ExcaliburDdd"
        TemplatePath = Join-Path $TemplatesDir "excalibur-ddd"
        Transports   = @()
        Databases    = @("sqlserver", "postgresql", "inmemory")
        HasDocker    = $true
        HasTests     = $true
        ExpectedFiles = @("Program.cs", "appsettings.json", "Domain/Aggregates/Order.cs", "Domain/Events/OrderCreated.cs", "Domain/Events/OrderShipped.cs", "Domain/ValueObjects/Money.cs", "Application/Commands/CreateOrderCommand.cs", "Application/Commands/CreateOrderCommandHandler.cs", "Application/Queries/GetOrderQuery.cs", "Application/Queries/GetOrderQueryHandler.cs")
    },
    @{
        ShortName    = "excalibur-cqrs"
        SourceName   = "Company.EventSourcingIntro"
        TemplatePath = Join-Path $TemplatesDir "excalibur-cqrs"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs")
        Databases    = @("sqlserver", "postgresql", "inmemory")
        HasDocker    = $true
        HasTests     = $true
        ExpectedFiles = @("Program.cs", "appsettings.json", "Domain/Aggregates/Order.cs", "Domain/Events/OrderCreated.cs", "Domain/Events/OrderShipped.cs", "Application/Commands/CreateOrderCommand.cs", "Application/Commands/CreateOrderCommandHandler.cs", "Application/Queries/GetOrderQuery.cs", "Application/Queries/GetOrderQueryHandler.cs", "ReadModel/OrderProjection.cs", "ReadModel/OrderReadModel.cs")
    },
    @{
        ShortName    = "dispatch-serverless"
        SourceName   = "Company.DispatchServerless"
        TemplatePath = Join-Path $TemplatesDir "dispatch-serverless"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs", "googlepubsub")
        Databases    = @()
        HasDocker    = $false
        HasTests     = $false
        ExpectedFiles = @("Program.cs", "Function.cs")
    },
    @{
        ShortName    = "dispatch-minimal-api"
        SourceName   = "Company.DispatchMinimalApi"
        TemplatePath = Join-Path $TemplatesDir "dispatch-minimal-api"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs", "googlepubsub")
        Databases    = @()
        HasDocker    = $true
        HasTests     = $false
        ExpectedFiles = @("Program.cs", "appsettings.json")
    },
    @{
        ShortName    = "excalibur-saga"
        SourceName   = "Company.ExcaliburSaga"
        TemplatePath = Join-Path $TemplatesDir "excalibur-saga"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs", "googlepubsub")
        Databases    = @("sqlserver", "inmemory")
        HasDocker    = $true
        HasTests     = $false
        ExpectedFiles = @("Program.cs", "appsettings.json", "Sagas/OrderSaga.cs", "Sagas/OrderSagaData.cs", "Messages/StartOrderProcessing.cs", "Messages/OrderEvents.cs")
    },
    @{
        ShortName    = "excalibur-outbox"
        SourceName   = "Company.ExcaliburOutbox"
        TemplatePath = Join-Path $TemplatesDir "excalibur-outbox"
        Transports   = @("inmemory", "kafka", "rabbitmq", "azureservicebus", "awssqs", "googlepubsub")
        Databases    = @("sqlserver", "inmemory")
        HasDocker    = $true
        HasTests     = $false
        ExpectedFiles = @("Program.cs", "appsettings.json", "Handlers/PlaceOrderHandler.cs", "Messages/PlaceOrderCommand.cs", "Messages/OrderPlacedEvent.cs")
    }
)

function Write-TestResult {
    param(
        [string]$TestName,
        [Parameter(Mandatory=$true)]$Passed,
        [string]$Details = ""
    )
    $Passed = [bool]$Passed
    $script:TestCount++
    if ($Passed) {
        $script:PassCount++
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
    }
    else {
        $script:FailCount++
        $script:Failures += "${TestName}: ${Details}"
        Write-Host "  [FAIL] $TestName" -ForegroundColor Red
        if ($Details) {
            Write-Host "         $Details" -ForegroundColor Yellow
        }
    }
}

function Test-TemplateInstallation {
    Write-Host "`n=== Template Installation ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        $shortName = $template.ShortName
        $templatePath = $template.TemplatePath

        # Uninstall first (ignore errors)
        dotnet new uninstall $templatePath 2>$null | Out-Null

        $output = (dotnet new install $templatePath --force 2>&1) | Out-String
        $installed = [bool]($output -match "Success.*installed")

        Write-TestResult "Install $shortName from local source" $installed $output
    }

    # Derived from $Templates -- the one list this suite iterates -- so a template added
    # there cannot be silently absent from this check. The hand-written list this
    # replaced enumerated seven of the eight and omitted dispatch-minimal-api, so that
    # template could have vanished from 'dotnet new list' with this check still green.
    # One result per template, so a failure names which one is missing rather than
    # collapsing all of them into a single boolean.
    $listOutput = dotnet new list 2>&1 | Out-String
    foreach ($template in $Templates) {
        $shortName = $template.ShortName
        $isListed = $listOutput -match [regex]::Escape($shortName)
        Write-TestResult "  $shortName visible in 'dotnet new list'" $isListed
    }
}

function Test-DefaultInstantiation {
    Write-Host "`n=== Default Options Instantiation ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        $shortName = $template.ShortName
        $projectName = "Test_${shortName}_default" -replace '-', '_'
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new $shortName -n $projectName -o $outputDir 2>&1 | Out-String
        $created = $output -match "was created successfully"

        Write-TestResult "Instantiate $shortName with defaults" $created $output

        if ($created) {
            # Verify expected files exist
            foreach ($expectedFile in $template.ExpectedFiles) {
                $filePath = Join-Path $outputDir $expectedFile
                $exists = Test-Path $filePath
                Write-TestResult "  $shortName has $expectedFile" $exists "File not found: $filePath"
            }

            # Verify .csproj exists with correct name
            $csprojPath = Join-Path $outputDir "$projectName.csproj"
            $csprojExists = Test-Path $csprojPath
            Write-TestResult "  $shortName has $projectName.csproj (name substitution)" $csprojExists

            # Verify no Docker files (default is false)
            $dockerExists = Test-Path (Join-Path $outputDir "Dockerfile")
            Write-TestResult "  $shortName excludes Dockerfile by default" (-not $dockerExists)

            # Verify no test project (default is false)
            $testDirPattern = Join-Path $outputDir "*.Tests"
            $testDirExists = (Get-ChildItem -Path $outputDir -Directory -Filter "*.Tests" -ErrorAction SilentlyContinue).Count -gt 0
            Write-TestResult "  $shortName excludes test project by default" (-not $testDirExists)

            # Verify sourceName replacement (no Company. references in generated code)
            $companyRefs = Get-ChildItem -Path $outputDir -Recurse -File -Filter "*.cs" |
                Select-String -Pattern $template.SourceName -SimpleMatch
            $noCompanyRefs = ($companyRefs | Measure-Object).Count -eq 0
            $companyRefDetails = ""
            if (-not $noCompanyRefs) { $companyRefDetails = "Found $($template.SourceName) in: $($companyRefs.Path -join ', ')" }
            Write-TestResult "  $shortName has no unreplaced sourceName references" $noCompanyRefs $companyRefDetails
        }
    }
}

function Test-TransportOptions {
    Write-Host "`n=== Transport Options (dispatch-api, dispatch-worker, excalibur-cqrs) ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        if ($template.Transports.Count -eq 0) { continue }

        $shortName = $template.ShortName

        foreach ($transport in $template.Transports) {
            $projectName = "Test_${shortName}_${transport}" -replace '-', '_'
            $outputDir = Join-Path $TestOutputDir $projectName

            if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

            $output = dotnet new $shortName -n $projectName -o $outputDir --Transport $transport 2>&1 | Out-String
            $created = $output -match "was created successfully"

            Write-TestResult "Instantiate $shortName --Transport $transport" $created $output

            if ($created) {
                # Verify .csproj has correct transport package reference for non-inmemory
                $csprojPath = Join-Path $outputDir "$projectName.csproj"
                if (Test-Path $csprojPath) {
                    $csprojContent = Get-Content $csprojPath -Raw

                    switch ($transport) {
                        "kafka" {
                            $hasRef = $csprojContent -match "Dispatch\.Transport\.Kafka"
                            Write-TestResult "  $shortName/$transport has Kafka package ref" $hasRef
                        }
                        "rabbitmq" {
                            $hasRef = $csprojContent -match "Dispatch\.Transport\.RabbitMQ"
                            Write-TestResult "  $shortName/$transport has RabbitMQ package ref" $hasRef
                        }
                        "azureservicebus" {
                            $hasRef = $csprojContent -match "Dispatch\.Transport\.AzureServiceBus"
                            Write-TestResult "  $shortName/$transport has AzureServiceBus package ref" $hasRef
                        }
                        "awssqs" {
                            $hasRef = $csprojContent -match "Dispatch\.Transport\.AwsSqs"
                            Write-TestResult "  $shortName/$transport has AwsSqs package ref" $hasRef
                        }
                        "googlepubsub" {
                            $hasRef = $csprojContent -match "Dispatch\.Transport\.GooglePubSub"
                            Write-TestResult "  $shortName/$transport has GooglePubSub package ref" $hasRef
                        }
                        "inmemory" {
                            # Should NOT have any transport-specific packages
                            $hasTransportRef = $csprojContent -match "Dispatch\.Transport\.(Kafka|RabbitMQ|AzureServiceBus|AwsSqs|GooglePubSub)"
                            Write-TestResult "  $shortName/$transport has NO transport package refs" (-not $hasTransportRef)
                        }
                    }

                    # Verify no preprocessor conditionals remain in generated code
                    $csFiles = Get-ChildItem -Path $outputDir -Recurse -File -Filter "*.cs"
                    $hasPreprocessor = $false
                    foreach ($f in $csFiles) {
                        $content = Get-Content $f.FullName -Raw
                        if ($content -match '#if \(Use(Kafka|RabbitMQ|AzureServiceBus|AwsSqs|GooglePubSub)\)') {
                            $hasPreprocessor = $true
                            break
                        }
                    }
                    Write-TestResult "  $shortName/$transport has no remaining #if conditionals in .cs" (-not $hasPreprocessor)

                    # Verify no XML conditionals remain in .csproj
                    $hasXmlConditional = $csprojContent -match '<!--#if'
                    Write-TestResult "  $shortName/$transport has no remaining XML conditionals in .csproj" (-not $hasXmlConditional)
                }
            }
        }
    }
}

function Test-DatabaseOptions {
    Write-Host "`n=== Database Options (excalibur-ddd, excalibur-cqrs) ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        if ($template.Databases.Count -eq 0) { continue }

        $shortName = $template.ShortName

        foreach ($database in $template.Databases) {
            $projectName = "Test_${shortName}_${database}" -replace '-', '_'
            $outputDir = Join-Path $TestOutputDir $projectName

            if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

            $output = dotnet new $shortName -n $projectName -o $outputDir --Database $database 2>&1 | Out-String
            $created = $output -match "was created successfully"

            Write-TestResult "Instantiate $shortName --Database $database" $created $output

            if ($created) {
                $csprojPath = Join-Path $outputDir "$projectName.csproj"
                if (Test-Path $csprojPath) {
                    $csprojContent = Get-Content $csprojPath -Raw

                    switch ($database) {
                        "sqlserver" {
                            # Different templates use different SqlServer packages
                            $hasRef = $csprojContent -match "Excalibur\.(EventSourcing|Saga|Outbox)\.SqlServer"
                            Write-TestResult "  $shortName/$database has SqlServer package ref" $hasRef
                        }
                        "postgresql" {
                            $hasRef = $csprojContent -match "Excalibur\.EventSourcing\.Postgres"
                            Write-TestResult "  $shortName/$database has Postgres package ref" $hasRef
                        }
                        "inmemory" {
                            $hasDbRef = $csprojContent -match "Excalibur\.(EventSourcing|Saga|Outbox)\.(SqlServer|Postgres)"
                            Write-TestResult "  $shortName/$database has NO database-specific package refs" (-not $hasDbRef)
                        }
                    }

                    # Verify no XML conditionals remain
                    $hasXmlConditional = $csprojContent -match '<!--#if'
                    Write-TestResult "  $shortName/$database has no remaining XML conditionals in .csproj" (-not $hasXmlConditional)
                }
            }
        }
    }
}

function Test-IncludeTestsOption {
    Write-Host "`n=== --IncludeTests Option ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        if (-not $template.HasTests) { continue }

        $shortName = $template.ShortName
        $projectName = "Test_${shortName}_with_tests" -replace '-', '_'
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new $shortName -n $projectName -o $outputDir --IncludeTests true 2>&1 | Out-String
        $created = $output -match "was created successfully"

        Write-TestResult "Instantiate $shortName --IncludeTests" $created $output

        if ($created) {
            # Verify test project directory exists
            $testDir = Join-Path $outputDir "$projectName.Tests"
            $testDirExists = Test-Path $testDir
            Write-TestResult "  $shortName test project directory exists" $testDirExists

            if ($testDirExists) {
                # Verify test .csproj exists
                $testCsproj = Join-Path $testDir "$projectName.Tests.csproj"
                $testCsprojExists = Test-Path $testCsproj
                Write-TestResult "  $shortName test .csproj exists" $testCsprojExists

                if ($testCsprojExists) {
                    $content = Get-Content $testCsproj -Raw
                    # Verify test project references xUnit
                    $hasXunit = $content -match "xunit"
                    Write-TestResult "  $shortName test project references xUnit" $hasXunit

                    # Verify test project references Shouldly
                    $hasShouldly = $content -match "Shouldly"
                    Write-TestResult "  $shortName test project references Shouldly" $hasShouldly

                    # Verify test project references FakeItEasy
                    $hasFakeItEasy = $content -match "FakeItEasy"
                    Write-TestResult "  $shortName test project references FakeItEasy" $hasFakeItEasy

                    # Verify project reference to main project
                    $hasProjectRef = $content -match "ProjectReference"
                    Write-TestResult "  $shortName test project has ProjectReference to main project" $hasProjectRef
                }

                # Verify at least one test file exists
                $testFiles = Get-ChildItem -Path $testDir -Recurse -File -Filter "*Should.cs"
                $hasTestFiles = ($testFiles | Measure-Object).Count -gt 0
                Write-TestResult "  $shortName has test files (*Should.cs)" $hasTestFiles
            }
        }
    }
}

function Test-IncludeDockerOption {
    Write-Host "`n=== --IncludeDocker Option ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        if (-not $template.HasDocker) { continue }

        $shortName = $template.ShortName
        $projectName = "Test_${shortName}_with_docker" -replace '-', '_'
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new $shortName -n $projectName -o $outputDir --IncludeDocker true 2>&1 | Out-String
        $created = $output -match "was created successfully"

        Write-TestResult "Instantiate $shortName --IncludeDocker" $created $output

        if ($created) {
            $dockerExists = Test-Path (Join-Path $outputDir "Dockerfile")
            Write-TestResult "  $shortName has Dockerfile" $dockerExists

            $dockerIgnoreExists = Test-Path (Join-Path $outputDir ".dockerignore")
            Write-TestResult "  $shortName has .dockerignore" $dockerIgnoreExists
        }
    }
}

function Test-FrameworkOption {
    Write-Host "`n=== --Framework Option (net10.0 only) ===" -ForegroundColor Cyan

    # Framework=net10.0 is the only supported target since .NET 8/9 multi-target
    # was removed from templates. This test verifies the default behaviour.
    $framework = "net10.0"
    $projectName = "Test_dispatch_api_$($framework -replace '\.', '_')"
    $outputDir = Join-Path $TestOutputDir $projectName

    if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

    $output = dotnet new dispatch-api -n $projectName -o $outputDir --Framework $framework 2>&1 | Out-String
    $created = $output -match "was created successfully"

    Write-TestResult "Instantiate dispatch-api --Framework $framework" $created $output

    if ($created) {
        $csprojPath = Join-Path $outputDir "$projectName.csproj"
        if (Test-Path $csprojPath) {
            $content = Get-Content $csprojPath -Raw
            $hasFramework = $content -match "<TargetFramework>$framework</TargetFramework>"
            Write-TestResult "  .csproj has TargetFramework=$framework" $hasFramework
        }
    }
}

function Test-TemplatePack {
    Write-Host "`n=== Template Pack ===" -ForegroundColor Cyan

    $packOutput = Join-Path $RepoRoot "artifacts" "template-pack"
    if (Test-Path $packOutput) { Remove-Item -Recurse -Force $packOutput }

    # Template pack requires special handling - use dotnet pack with --no-build is not available
    # Instead verify the .csproj is valid for packing
    $csprojPath = Join-Path $TemplatesDir "Excalibur.Dispatch.Templates.csproj"
    $csprojExists = Test-Path $csprojPath
    Write-TestResult "Templates .csproj exists" $csprojExists

    if ($csprojExists) {
        $content = Get-Content $csprojPath -Raw
        $isTemplatePkg = $content -match "<PackageType>Template</PackageType>"
        Write-TestResult "  .csproj has PackageType=Template" $isTemplatePkg

        $hasContent = $content -match "<IncludeContentInPack>true</IncludeContentInPack>"
        Write-TestResult "  .csproj includes content in pack" $hasContent

        $noBuildOutput = $content -match "<IncludeBuildOutput>false</IncludeBuildOutput>"
        Write-TestResult "  .csproj excludes build output" $noBuildOutput

        # Derived from $Templates for the same reason as the visibility check above: a
        # hand-written copy of the template list drifts out of date silently.
        foreach ($tmpl in $Templates.ShortName) {
            $included = $content -match "Content Include=`"$tmpl"
            Write-TestResult "  .csproj includes $tmpl content" $included
        }
    }
}

function Test-NameSubstitution {
    Write-Host "`n=== Name Substitution (sourceName) ===" -ForegroundColor Cyan

    # Test with a custom name to verify sourceName replacement
    $customName = "Acme.MyProject"
    $projectName = $customName -replace '\.', '_'
    $outputDir = Join-Path $TestOutputDir "Test_name_substitution"

    if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

    $output = dotnet new dispatch-api -n $customName -o $outputDir 2>&1 | Out-String
    $created = $output -match "was created successfully"

    Write-TestResult "Instantiate dispatch-api -n $customName" $created $output

    if ($created) {
        # Verify .csproj name
        $csprojPath = Join-Path $outputDir "$customName.csproj"
        Write-TestResult "  Project file renamed to $customName.csproj" (Test-Path $csprojPath)

        # Verify namespace in .cs files
        $csFiles = Get-ChildItem -Path $outputDir -Recurse -File -Filter "*.cs"
        $allNamespacesCorrect = $true
        foreach ($f in $csFiles) {
            $content = Get-Content $f.FullName -Raw
            if ($content -match "namespace\s+" -and $content -match "Company\.DispatchApi") {
                $allNamespacesCorrect = $false
                break
            }
        }
        Write-TestResult "  All namespaces use $customName (no Company.DispatchApi)" $allNamespacesCorrect
    }
}

function Test-CqrsCombinations {
    Write-Host "`n=== excalibur-cqrs Combined Options (Transport + Database) ===" -ForegroundColor Cyan

    # Test representative combinations
    $combos = @(
        @{ Transport = "kafka"; Database = "sqlserver" },
        @{ Transport = "rabbitmq"; Database = "postgresql" },
        @{ Transport = "inmemory"; Database = "inmemory" }
    )

    foreach ($combo in $combos) {
        $transport = $combo.Transport
        $database = $combo.Database
        $projectName = "Test_cqrs_${transport}_${database}"
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new excalibur-cqrs -n $projectName -o $outputDir --Transport $transport --Database $database 2>&1 | Out-String
        $created = $output -match "was created successfully"

        Write-TestResult "Instantiate excalibur-cqrs --Transport $transport --Database $database" $created $output

        if ($created) {
            $csprojPath = Join-Path $outputDir "$projectName.csproj"
            if (Test-Path $csprojPath) {
                $content = Get-Content $csprojPath -Raw
                $hasXmlConditional = $content -match '<!--#if'
                Write-TestResult "  cqrs/$transport+$database has no XML conditionals" (-not $hasXmlConditional)
            }
        }
    }
}

function Test-DockerFrameworkTags {
    Write-Host "`n=== AC-Docker: Dockerfile Framework Tag Validation ===" -ForegroundColor Cyan

    # Templates only target net10.0 since .NET 8/9 multi-target was dropped.
    $framework = "net10.0"
    $expectedTag = $framework -replace 'net', ''

    foreach ($template in $Templates) {
        if (-not $template.HasDocker) { continue }

        $shortName = $template.ShortName
        $projectName = "Test_${shortName}_docker_$($framework -replace '\.', '_')" -replace '-', '_'
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new $shortName -n $projectName -o $outputDir --Framework $framework --IncludeDocker true 2>&1 | Out-String
        $created = $output -match "was created successfully"

        Write-TestResult "Instantiate $shortName --Framework $framework --IncludeDocker" $created $output

        if ($created) {
            $dockerfilePath = Join-Path $outputDir "Dockerfile"
            $dockerExists = Test-Path $dockerfilePath

            Write-TestResult "  $shortName/$framework has Dockerfile" $dockerExists

            if ($dockerExists) {
                $dockerContent = Get-Content $dockerfilePath -Raw

                # Verify SDK image tag matches framework
                $hasSdkTag = $dockerContent -match "mcr\.microsoft\.com/dotnet/sdk:$expectedTag"
                Write-TestResult "  $shortName/$framework Dockerfile has sdk:$expectedTag" $hasSdkTag

                # Verify runtime/aspnet image tag matches framework
                # dispatch-worker uses runtime, others use aspnet
                if ($shortName -eq "dispatch-worker") {
                    $hasRuntimeTag = $dockerContent -match "mcr\.microsoft\.com/dotnet/runtime:$expectedTag"
                    Write-TestResult "  $shortName/$framework Dockerfile has runtime:$expectedTag" $hasRuntimeTag
                }
                else {
                    $hasAspnetTag = $dockerContent -match "mcr\.microsoft\.com/dotnet/aspnet:$expectedTag"
                    Write-TestResult "  $shortName/$framework Dockerfile has aspnet:$expectedTag" $hasAspnetTag
                }

                # Verify NO hardcoded stale framework tags (8.0/9.0)
                $hasStaleTag = $dockerContent -match "dotnet/(sdk|aspnet|runtime):(8|9)\.0"
                Write-TestResult "  $shortName/$framework Dockerfile has NO stale 8.0/9.0 tags" (-not $hasStaleTag)
            }
        }
    }
}

function Test-PatternMatchingInDdd {
    Write-Host "`n=== DDD Pattern Matching Validation ===" -ForegroundColor Cyan

    $projectName = "Test_ddd_pattern_matching"
    $outputDir = Join-Path $TestOutputDir $projectName

    if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

    dotnet new excalibur-ddd -n $projectName -o $outputDir 2>&1 | Out-Null

    $orderFile = Join-Path $outputDir "Domain" "Aggregates" "Order.cs"
    if (Test-Path $orderFile) {
        $content = Get-Content $orderFile -Raw

        # Must use pattern matching (switch expression)
        $usesPatternMatching = $content -match '@event\s+switch'
        Write-TestResult "DDD Order aggregate uses pattern matching (switch expression)" $usesPatternMatching

        # Must NOT use reflection for event application (GetType().Name in error messages is OK)
        $usesReflection = $content -match 'Activator\.Create|MethodInfo|\.Invoke\(|Assembly\.GetTypes|Type\.GetMethod'
        Write-TestResult "DDD Order aggregate does NOT use reflection for event application" (-not $usesReflection)

        # Must extend AggregateRoot
        $extendsAggregateRoot = $content -match 'class\s+Order\s*:\s*AggregateRoot'
        Write-TestResult "DDD Order extends AggregateRoot" $extendsAggregateRoot
    }
    else {
        Write-TestResult "DDD Order.cs exists" $false "File not found: $orderFile"
    }
}

function Invoke-Cleanup {
    Write-Host "`n=== Cleanup ===" -ForegroundColor Cyan

    # Uninstall templates
    foreach ($template in $Templates) {
        dotnet new uninstall $template.TemplatePath 2>$null | Out-Null
    }

    if (-not $NoCleanUp) {
        if (Test-Path $TestOutputDir) {
            Remove-Item -Recurse -Force $TestOutputDir
            Write-Host "  Cleaned up test output directory" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  Test output preserved at: $TestOutputDir" -ForegroundColor Gray
    }
}

# A template default is a PROMISE about which build a consumer gets from `dotnet new`. The build check
# above proves the default RESOLVES; it cannot prove the default is still the RIGHT one, because a
# prerelease-pinned default keeps resolving and keeps building long after a stable release exists. That
# is the gap this check closes, and it is why the character-class gate that once guarded it was deleted
# rather than kept: "contains a hyphen" is a property of the FILE, and the requirement is a question
# about the FEED.
#
# Split deliberately into a pure decision and a network fetch, so the decision is provable without the
# network and the arms below can plant the very condition that makes a prerelease pin wrong.
function Test-DefaultIsStaleAgainstFeed {
    param(
        [Parameter(Mandatory = $true)][string]   $DefaultRange,
        [Parameter(Mandatory = $true)][string[]] $PublishedVersions
    )

    # Only a prerelease-pinned default can go stale this way; a stable float already tracks releases.
    if ($DefaultRange -notmatch '-') { return $false }

    $major = ($DefaultRange -split '\.')[0]
    if ($major -notmatch '^\d+$') { return $false }

    # A stable release is one carrying no prerelease label. If any exists in the same major line, the
    # template is still handing consumers a prerelease when a release is available.
    foreach ($v in $PublishedVersions) {
        if ($v -notmatch '-' -and ($v -split '\.')[0] -eq $major) { return $true }
    }
    return $false
}

function Get-PublishedVersions {
    param([Parameter(Mandatory = $true)][string] $PackageId)
    try {
        $uri = "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLowerInvariant())/index.json"
        return (Invoke-RestMethod -Uri $uri -TimeoutSec 30 -ErrorAction Stop).versions
    }
    catch {
        return $null
    }
}

function Test-TemplateDefaultsTrackTheFeed {
    Write-Host "`n=== Template Defaults vs the Published Feed ===" -ForegroundColor Cyan

    # Both arms of the decision, on synthetic inputs, so this check is never merely green-because-true-today.
    # The second arm plants the condition the requirement is about -- a stable release existing -- which is
    # the only thing that makes a prerelease pin wrong, and cannot be planted by editing a template.
    $syntheticNoStable = @("10.0.0-alpha.9", "10.0.0-alpha.10")
    $syntheticWithStable = @("10.0.0-alpha.10", "10.0.0")
    Write-TestResult "decision: prerelease pin is correct while no stable exists" `
        (-not (Test-DefaultIsStaleAgainstFeed -DefaultRange "10.0.0-*" -PublishedVersions $syntheticNoStable))
    Write-TestResult "decision: prerelease pin is STALE once a stable ships (planted)" `
        (Test-DefaultIsStaleAgainstFeed -DefaultRange "10.0.0-*" -PublishedVersions $syntheticWithStable)
    Write-TestResult "decision: a stable float is never reported stale" `
        (-not (Test-DefaultIsStaleAgainstFeed -DefaultRange "10.*" -PublishedVersions $syntheticWithStable))

    $published = Get-PublishedVersions -PackageId "Excalibur.Dispatch"
    if ($null -eq $published -or $published.Count -eq 0) {
        # Fail closed. A check that could not reach the feed has not established that the defaults are
        # current, and reporting that as a pass is how a gate earns a green it did not earn.
        Write-TestResult "feed reachable to judge template defaults" $false `
            "could not read the published version list; the defaults were NOT evaluated"
        return
    }

    $stable = @($published | Where-Object { $_ -notmatch '-' })
    Write-Host "  Published: $($published.Count) version(s), $($stable.Count) stable" -ForegroundColor Gray

    foreach ($file in (Get-ChildItem -Path $TemplatesDir -Recurse -Filter "*.csproj")) {
        $content = Get-Content $file.FullName -Raw
        foreach ($m in [regex]::Matches($content, '<ExcaliburDispatchVersion[^>]*>([^<]+)</ExcaliburDispatchVersion>')) {
            $range = $m.Groups[1].Value.Trim()
            $stale = Test-DefaultIsStaleAgainstFeed -DefaultRange $range -PublishedVersions $published
            Write-TestResult "  $($file.Name) default '$range' still matches the feed" (-not $stale) `
                $(if ($stale) { "a stable release exists in this major line, so scaffolds still hand consumers a prerelease" } else { "" })
        }
    }
}
function Test-ScaffoldBuilds {
    Write-Host "`n=== Scaffold Restore + Build (against the feed a consumer uses) ===" -ForegroundColor Cyan

    # THIS IS THE CHECK WHOSE ABSENCE MADE EVERYTHING ELSE MEANINGLESS. The rest of
    # this suite verifies STRUCTURE: files exist, package references are present as
    # TEXT, sourceName was substituted. None of that compiles anything, so a version
    # range resolving to nothing, a renamed package, or an API break all pass. This
    # suite once reported 396 passed while every scaffold was unrestorable, and later
    # while all eight scaffolds failed to compile against the published packages.
    #
    # BUILT OUTSIDE THE REPOSITORY, DELIBERATELY. The other tests scaffold into
    # artifacts/ under the repo root, where Directory.Build.props applies -- repo-wide
    # analyzers, warning levels and properties that no consumer has. A build that
    # passes under those is not evidence a consumer's build passes, and could just as
    # easily fail on rules a consumer never sees. A temp directory outside the tree is
    # the only place this measures what a consumer actually gets.
    #
    # dotnet build restores implicitly, so an unresolvable version range fails here
    # too -- restore and compile are both covered by the one check.
    $buildRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("xlb-tmpl-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    Write-Host "  Build sandbox: $buildRoot" -ForegroundColor Gray

    try {
        foreach ($template in $Templates) {
            $shortName = $template.ShortName
            $projectName = "Build_${shortName}" -replace '-', '_'
            $outputDir = Join-Path $buildRoot $projectName

            $newOut = dotnet new $shortName -n $projectName -o $outputDir 2>&1 | Out-String
            if ($newOut -notmatch "was created successfully") {
                Write-TestResult "$shortName scaffolds for the build check" $false $newOut
                continue
            }

            $buildOut = dotnet build $outputDir -c Release --nologo 2>&1 | Out-String
            $errorLines = @($buildOut -split "`r?`n" | Where-Object { $_ -match "error [A-Z]+\d+" } | Select-Object -Unique)
            $built = ($errorLines.Count -eq 0)
            $detail = if ($built) { "" } else { (($errorLines | Select-Object -First 3) -join " | ") }

            Write-TestResult "$shortName scaffold builds against the published feed" $built $detail
        }
    }
    finally {
        if (Test-Path $buildRoot) {
            Remove-Item -Recurse -Force $buildRoot -ErrorAction SilentlyContinue
        }
    }
}

# ============================================================
# Main Execution
# ============================================================

Write-Host "============================================" -ForegroundColor White
Write-Host " Excalibur Template CI Validation" -ForegroundColor White
Write-Host " template acceptance criteria" -ForegroundColor Gray
Write-Host "============================================" -ForegroundColor White
Write-Host ""
Write-Host "Templates dir: $TemplatesDir" -ForegroundColor Gray
Write-Host "Test output:   $TestOutputDir" -ForegroundColor Gray

# Ensure output directory
if (-not (Test-Path $TestOutputDir)) {
    New-Item -ItemType Directory -Path $TestOutputDir -Force | Out-Null
}

try {
    Test-TemplateInstallation
    Test-DefaultInstantiation
    Test-ScaffoldBuilds
    Test-TemplateDefaultsTrackTheFeed
    Test-TransportOptions
    Test-DatabaseOptions
    Test-IncludeTestsOption
    Test-IncludeDockerOption
    Test-FrameworkOption
    Test-TemplatePack
    Test-NameSubstitution
    Test-CqrsCombinations
    Test-DockerFrameworkTags
    Test-PatternMatchingInDdd
}
finally {
    Invoke-Cleanup
}

# ============================================================
# Summary
# ============================================================

Write-Host ""
Write-Host "============================================" -ForegroundColor White
Write-Host " RESULTS" -ForegroundColor White
Write-Host "============================================" -ForegroundColor White
Write-Host "  Total:  $script:TestCount" -ForegroundColor White
Write-Host "  Passed: $script:PassCount" -ForegroundColor Green
Write-Host "  Failed: $script:FailCount" -ForegroundColor $(if ($script:FailCount -gt 0) { "Red" } else { "Green" })

if ($script:FailCount -gt 0) {
    Write-Host ""
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($failure in $script:Failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}
else {
    Write-Host ""
    Write-Host "All template CI validation tests passed!" -ForegroundColor Green
    exit 0
}
