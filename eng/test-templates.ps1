#!/usr/bin/env pwsh
<#
.SYNOPSIS
    CI smoke tests for Excalibur dotnet new templates.
    Validates template installation, instantiation with all option combinations,
    and project structure correctness.

.DESCRIPTION
    Covers the template acceptance criteria:
    - Packs this tree and installs the artifact in a fresh custom template hive
    - Compiles every finite manifest parameter combination outside the repository
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
    [switch]$NoCleanUp,

    # Folder of locally-packed .nupkg files. Supplied, the scaffold build resolves OUR packages from
    # here instead of nuget.org, which is what makes this check answer "is this COMMIT correct?".
    # Omitted, it builds against the published feed and answers "can a consumer scaffold TODAY?".
    # Both questions are real; they diverge for the whole window between a breaking change and the
    # next publish, and a gate wired to the second one is red-by-construction during that window.
    [string]$LocalFeed = '',

    # The exact version present in -LocalFeed. Required with it, and the reason is subtle: the
    # templates float 10.0.0-*, and NuGet picks the highest match ACROSS ALL SOURCES -- so a published
    # alpha newer than the local pack silently wins and the check measures the feed again while
    # appearing to measure the tree. Pinning the exact locally-packed version removes the ambiguity.
    [string]$LocalFeedVersion = ''
)

$ErrorActionPreference = 'Stop'
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0
$script:Failures = @()
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'package-composition.functions.ps1')
$TemplateHiveArguments = @()
$TemplateVariants = @()

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
$runId = [Guid]::NewGuid().ToString('N')
$TestOutputDir = Join-Path $TestOutputDir $runId
$TemplateEvidenceDir = Join-Path $RepoRoot "artifacts/template-evidence/$runId"
[void][IO.Directory]::CreateDirectory($TemplateEvidenceDir)
$TemplateHive = Join-Path ([IO.Path]::GetTempPath()) "xlb-template-hive-$runId"
$TemplateHiveArguments = @('--debug:custom-hive', $TemplateHive)
$originalNugetPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $TemplateEvidenceDir 'package-cache'
$script:TemplateCandidateSha = (git -C $RepoRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot establish template candidate HEAD.' }
$script:RuntimeSourceIdentity = Get-CompositionSourceIdentity $RepoRoot
$script:RuntimeSourceIdentity | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'runtime-source-identity.json') -Encoding utf8
$script:CandidatePackageHashes = @{}
if ($LocalFeed) {
    foreach ($package in @(Get-ChildItem -LiteralPath $LocalFeed -Filter '*.nupkg' -File)) {
        $script:CandidatePackageHashes[$package.Name.ToLowerInvariant()] = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
    }
    $script:CandidatePackageHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'candidate-package-hashes.json') -Encoding utf8
}
$inputPaths = @(git -C $RepoRoot ls-files --cached --others --exclude-standard -- templates eng/test-templates.ps1 eng/test-templates.test.ps1 | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate template candidate inputs.' }
$script:TemplateInputHashes = @($inputPaths | ForEach-Object { @{path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $RepoRoot $_) -Algorithm SHA256).Hash} })
$script:TemplateInputHashes |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'source-inputs.json') -Encoding utf8

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

function Get-TemplateVariants {
    param($Definitions)
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($definition in $Definitions) {
        $manifest = Get-Content -LiteralPath (Join-Path $definition.TemplatePath '.template.config/template.json') -Raw | ConvertFrom-Json
        $combinations = @(@{})
        foreach ($symbol in $manifest.symbols.PSObject.Properties) {
            if ($symbol.Value.type -ne 'parameter') { continue }
            $values = switch ($symbol.Value.datatype) {
                'choice' { @($symbol.Value.choices | ForEach-Object choice) }
                'bool' { @('false', 'true') }
                default { throw "Unsupported finite template parameter: $($manifest.shortName)/$($symbol.Name)" }
            }
            if (-not $values.Count) { throw "Empty template parameter: $($symbol.Name)" }
            $next = [Collections.Generic.List[object]]::new()
            foreach ($combination in $combinations) {
                foreach ($value in $values) {
                    $copy = @{}; foreach ($key in $combination.Keys) { $copy[$key] = $combination[$key] }
                    $copy[$symbol.Name] = [string]$value; $next.Add($copy)
                }
            }
            $combinations = @($next.ToArray())
        }
        foreach ($combination in $combinations) {
            $arguments = @(); foreach ($key in @($combination.Keys | Sort-Object)) { $arguments += @("--$key", $combination[$key]) }
            $rows.Add([pscustomobject]@{ ShortName = $manifest.shortName; Parameters = $combination; Arguments = $arguments })
        }
    }
    return $rows.ToArray()
}

function Save-TemplateNativeReceipt {
    param([string]$Directory, [string]$Stage, [string[]]$Arguments, [int]$NativeExit, [string]$Output)
    [void][IO.Directory]::CreateDirectory($Directory)
    $Output | Set-Content -LiteralPath (Join-Path $Directory "$Stage.log") -Encoding utf8
    @{ stage=$Stage; arguments=$Arguments; exitCode=$NativeExit; candidateSha=$script:TemplateCandidateSha;
       templatePackageHash=$script:TemplatePackageHash; sourceFingerprint=$script:RuntimeSourceIdentity.sha256;
       mode=$(if ($LocalFeed) {'candidate'} else {'published-feed'}); producerProvenance='external-P06-manifest-required-for-current-source-pack-claim' } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Directory "$Stage.receipt.json") -Encoding utf8
}

function Assert-TemplatePackageConsumption {
    param([string]$Project, [string]$Directory)
    $assetsPath = Join-Path (Split-Path $Project) 'obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    $consumed = @()
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'package' -or $library.Name -notlike 'Excalibur.*/*') { continue }
        $parts = $library.Name.Split('/'); $id=$parts[0]; $version=$parts[1]
        $package = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $path = Join-Path (Join-Path $folder $library.Value.path) ("$id.$version.nupkg".ToLowerInvariant())
            if (Test-Path -LiteralPath $path) { $package = $path; break }
        }
        if (-not $package) { throw "Cannot hash consumed package $($library.Name)" }
        $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
        if ($LocalFeed) {
            if ($version -ne $LocalFeedVersion) { throw "Wrong candidate version: $($library.Name)" }
            $expectedName = "$id.$version.nupkg".ToLowerInvariant()
            if (-not $script:CandidatePackageHashes.ContainsKey($expectedName) -or $script:CandidatePackageHashes[$expectedName] -ne $hash) {
                throw "Consumed package differs from local candidate: $($library.Name)"
            }
        }
        $consumed += @{id=$id; version=$version; sha256=$hash; path=$package}
    }
    if ($LocalFeed -and -not $consumed.Count) { throw 'Candidate consumer restored no Excalibur packages.' }
    $consumed | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Directory 'packages.json') -Encoding utf8
}

function Test-TemplateInstallation {
    Write-Host "`n=== Fresh template artifact installation ===" -ForegroundColor Cyan
    $packDirectory = Join-Path $TemplateEvidenceDir 'template-pack'
    [void][IO.Directory]::CreateDirectory($packDirectory)
    $packArgs = @('pack', (Join-Path $TemplatesDir 'Excalibur.Dispatch.Templates.csproj'), '-c', 'Release', '-o', $packDirectory, '--nologo')
    $output = dotnet @packArgs 2>&1 | Out-String
    $nativeExit = $LASTEXITCODE
    Save-TemplateNativeReceipt $packDirectory 'pack' $packArgs $nativeExit $output
    if ($nativeExit -ne 0) { throw "Template pack failed: native exit $nativeExit. See $packDirectory" }
    $packages = @(Get-ChildItem -LiteralPath $packDirectory -Filter '*.nupkg')
    if ($packages.Count -ne 1) { throw 'Expected exactly one freshly packed template artifact.' }
    $script:TemplatePackageHash = (Get-FileHash -LiteralPath $packages[0].FullName -Algorithm SHA256).Hash
    $archive = [IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
    try {
        $specs = @($archive.Entries | Where-Object FullName -match '^[^/]+\.nuspec$')
        if ($specs.Count -ne 1) { throw 'Template artifact must contain exactly one root nuspec.' }
        $reader = [IO.StreamReader]::new($specs[0].Open())
        try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if (-not $spec.SelectSingleNode("//*[local-name()='packageType' and @name='Template']")) { throw 'Packed artifact is not a Template package.' }
        if (@($archive.Entries | Where-Object FullName -match '^(lib|ref)/.*\.dll$').Count) { throw 'Template artifact unexpectedly contains runtime assemblies.' }
        $manifests = @($archive.Entries | Where-Object FullName -like '*/.template.config/template.json')
        if ($manifests.Count -ne $Templates.Count) { throw 'Packed template manifest roster differs from declared templates.' }
        foreach ($template in $Templates) {
            $entry = @($manifests | Where-Object FullName -like "*/$($template.ShortName)/.template.config/template.json")
            if ($entry.Count -ne 1) { throw "Missing/duplicate packed manifest for $($template.ShortName)" }
            $reader = [IO.StreamReader]::new($entry[0].Open())
            try { $packed = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $source = Get-Content -LiteralPath (Join-Path $template.TemplatePath '.template.config/template.json') -Raw
            if ($packed -cne $source) { throw "Packed manifest differs from candidate: $($template.ShortName)" }
        }
    } finally { $archive.Dispose() }
    $script:TemplateVariants = @(Get-TemplateVariants $Templates)
    $script:TemplateVariants | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'variant-plan.json') -Encoding utf8
    $installArgs = @('new', 'install', $packages[0].FullName) + $TemplateHiveArguments
    $output = dotnet @installArgs 2>&1 | Out-String
    $nativeExit = $LASTEXITCODE
    Save-TemplateNativeReceipt $packDirectory 'install' $installArgs $nativeExit $output
    if ($nativeExit -ne 0) { throw "Template artifact install failed: native exit $nativeExit" }
    $listOutput = dotnet new list @TemplateHiveArguments 2>&1 | Out-String
    $nativeExit = $LASTEXITCODE
    if ($nativeExit -ne 0) { throw "Template listing failed: native exit $nativeExit" }
    foreach ($template in $Templates) {
        Write-TestResult "$($template.ShortName) visible after artifact installation" ($listOutput -match [regex]::Escape($template.ShortName)) $listOutput
    }
}
function Test-DefaultInstantiation {
    Write-Host "`n=== Default Options Instantiation ===" -ForegroundColor Cyan

    foreach ($template in $Templates) {
        $shortName = $template.ShortName
        $projectName = "Test_${shortName}_default" -replace '-', '_'
        $outputDir = Join-Path $TestOutputDir $projectName

        if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

        $output = dotnet new $shortName -n $projectName -o $outputDir @TemplateHiveArguments 2>&1 | Out-String
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

            $output = dotnet new $shortName -n $projectName -o $outputDir --Transport $transport @TemplateHiveArguments 2>&1 | Out-String
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

            $output = dotnet new $shortName -n $projectName -o $outputDir --Database $database @TemplateHiveArguments 2>&1 | Out-String
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

        $output = dotnet new $shortName -n $projectName -o $outputDir --IncludeTests true @TemplateHiveArguments 2>&1 | Out-String
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

        $output = dotnet new $shortName -n $projectName -o $outputDir --IncludeDocker true @TemplateHiveArguments 2>&1 | Out-String
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

    $output = dotnet new dispatch-api -n $projectName -o $outputDir --Framework $framework @TemplateHiveArguments 2>&1 | Out-String
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

    # Artifact packing/install has already executed; retain these source-shape checks as diagnostics.
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

    $output = dotnet new dispatch-api -n $customName -o $outputDir @TemplateHiveArguments 2>&1 | Out-String
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

        $output = dotnet new excalibur-cqrs -n $projectName -o $outputDir --Transport $transport --Database $database @TemplateHiveArguments 2>&1 | Out-String
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

        $output = dotnet new $shortName -n $projectName -o $outputDir --Framework $framework --IncludeDocker true @TemplateHiveArguments 2>&1 | Out-String
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

    dotnet new excalibur-ddd -n $projectName -o $outputDir @TemplateHiveArguments 2>&1 | Out-Null

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
    # The only installed templates belong to this run's custom hive. Never uninstall user templates.
    try {
        $afterSha = (git -C $RepoRoot rev-parse HEAD | Out-String).Trim()
        $headExit = $LASTEXITCODE
        if ($headExit -ne 0 -or $afterSha -ne $script:TemplateCandidateSha) {
            Write-TestResult 'Candidate HEAD remained unchanged' $false $afterSha
        }
        $afterInputs = @(git -C $RepoRoot ls-files --cached --others --exclude-standard -- templates eng/test-templates.ps1 eng/test-templates.test.ps1 | Sort-Object -Unique)
        $inputsExit = $LASTEXITCODE
        if ($inputsExit -ne 0 -or ($afterInputs -join '|') -cne (($script:TemplateInputHashes.path) -join '|')) {
            Write-TestResult 'Template input roster remained unchanged' $false 'Template input paths changed.'
        }
        foreach ($input in $script:TemplateInputHashes) {
            $path = Join-Path $RepoRoot $input.path
            if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $input.sha256) {
                Write-TestResult 'Template input remained unchanged' $false $input.path
            }
        }
        if ((Get-CompositionSourceIdentity $RepoRoot).sha256 -ne $script:RuntimeSourceIdentity.sha256) {
            Write-TestResult 'Candidate source remained unchanged' $false 'Runtime/build inputs changed during template verification.'
        }
        if ($LocalFeed) {
            foreach ($name in $script:CandidatePackageHashes.Keys) {
                $path = Join-Path $LocalFeed $name
                if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $script:CandidatePackageHashes[$name]) {
                    Write-TestResult 'Candidate package remained unchanged' $false $name
                }
            }
        }
        if (-not $NoCleanUp -and $script:FailCount -eq 0) {
            Remove-TemplateTemporaryDirectory $TemplateHive
            $expectedParent = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
            $resolved = [IO.Path]::GetFullPath($TestOutputDir)
            if (-not $resolved.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or (Get-Item -LiteralPath $resolved).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe structural output cleanup path.' }
            Assert-TemplateCleanupTree $resolved
            Remove-Item -LiteralPath $resolved -Recurse -Force
        } else { Write-Host "Template outputs/hive preserved: $TestOutputDir; $TemplateHive" }
    } catch {
        Write-TestResult 'Template final validation/cleanup' $false $_.Exception.ToString()
    } finally {
        $env:NUGET_PACKAGES = $originalNugetPackages
        $finalStatus = if ($script:TemplateRunCompleted -and $script:FailCount -eq 0 -and $TemplateVariants.Count -gt 0 -and $script:CompletedTemplateVariants -eq $TemplateVariants.Count) {'passed'} else {'failed-or-incomplete'}
        $finalReceipt = @{candidateSha=$script:TemplateCandidateSha; sourceFingerprint=$script:RuntimeSourceIdentity.sha256;
            templatePackageHash=$script:TemplatePackageHash; plannedVariants=$TemplateVariants.Count;
            completedVariants=$script:CompletedTemplateVariants; failures=$script:FailCount;
            runCompleted=$script:TemplateRunCompleted; status=$finalStatus;
            mode=$(if ($LocalFeed) {'candidate'} else {'published-feed'}); producerProvenance='external-P06-manifest-required-for-current-source-pack-claim'}
        $finalReceipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'matrix.receipt.json') -Encoding utf8
        $finalReceipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'gate.receipt.json') -Encoding utf8
    }
    Write-Host "Native receipts and variant plan retained at $TemplateEvidenceDir"
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
function Assert-TemplateDatabaseSelection {
    param([string]$OutputDirectory, [object]$Variant)
    $concern = switch ($Variant.ShortName) {
        'excalibur-ddd' { 'EventSourcing' }
        'excalibur-cqrs' { 'EventSourcing' }
        'excalibur-saga' { 'Saga' }
        'excalibur-outbox' { 'Outbox' }
        default { return $null }
    }
    if (-not $Variant.Parameters.ContainsKey('Database')) { throw 'Database variant has no selected Database parameter.' }
    $database = $Variant.Parameters['Database']
    if ($database -notin @('sqlserver','postgresql','inmemory')) { throw "Unknown database selection: $database" }
    $configurationText = Get-Content -LiteralPath (Join-Path $OutputDirectory 'appsettings.json') -Raw
    $json = [System.Text.Json.JsonDocument]::Parse($configurationText, [System.Text.Json.JsonDocumentOptions]::new())
    try {
        if ($configurationText -match '//#(if|elseif|endif)') { throw 'Unprocessed configuration template directive.' }
        $connections = $json.RootElement.GetProperty('ConnectionStrings')
        if ($connections.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'ConnectionStrings must be an object.' }
        $entries = @($connections.EnumerateObject())
        $storeName = switch ($concern) { 'EventSourcing' { 'EventStore' } 'Saga' { 'SagaStore' } 'Outbox' { 'OutboxStore' } }
        if ($database -eq 'inmemory') {
            if ($entries.Count -ne 0) { throw 'In-memory variant emitted a database connection string.' }
        } else {
            if ($entries.Count -ne 1 -or $entries[0].Name -cne $storeName) { throw "Expected only the $storeName connection string." }
            $connection = $entries[0].Value.GetString()
            $expectedPrefix = if ($database -eq 'postgresql') { 'Host=localhost;Database=' } else { 'Server=localhost;Database=' }
            if ([string]::IsNullOrWhiteSpace($connection) -or -not $connection.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
                throw "Connection string does not select $database."
            }
            if ($database -eq 'postgresql' -and ($connection -notmatch ';Username=postgres;' -or $connection -notmatch ';Password=CHANGE_ME$')) {
                throw 'PostgreSQL starter connection must include its documented placeholder credentials.'
            }
        }
    } finally { $json.Dispose() }
    $projects = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.csproj' -File)
    if ($projects.Count -ne 1) { throw 'Expected exactly one root scaffold project.' }
    [xml]$project = Get-Content -LiteralPath $projects[0].FullName -Raw
    $suffix = switch ($database) { 'sqlserver' { 'SqlServer' } 'postgresql' { 'Postgres' } 'inmemory' { 'InMemory' } }
    $expectedPackage = if ($concern -eq 'Saga' -and $database -eq 'inmemory') { 'Excalibur.Saga' } else { "Excalibur.$concern.$suffix" }
    $packages = @($project.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { $_.GetAttribute('Include') })
    if (@($packages | Where-Object { $_ -ceq $expectedPackage }).Count -ne 1) { throw "Missing or duplicated selected provider package $expectedPackage." }
    $providerPackages = @($packages | Where-Object { $_ -match ('^Excalibur\.' + $concern + '\.(SqlServer|Postgres|InMemory)$') })
    if (@($providerPackages | Where-Object { $_ -cne $expectedPackage }).Count -ne 0) { throw 'Scaffold also references an unselected database provider.' }
    $program = Get-Content -LiteralPath (Join-Path $OutputDirectory 'Program.cs') -Raw
    # Mask comments and ordinary/verbatim string literals while retaining offsets. This observer is
    # scoped to the current scaffold sources; invocation-looking prose is not an executing provider.
    $nonCode = '@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//[^\r\n]*|/\*[\s\S]*?\*/'
    $code = [regex]::Replace($program, $nonCode, { param($match) ' ' * $match.Length })
    $method = switch ($database) { 'sqlserver' { 'UseSqlServer' } 'postgresql' { 'UsePostgres' } 'inmemory' { if ($concern -eq 'Saga') { 'WithInMemoryStore' } else { 'UseInMemory' } } }
    if ($code -notmatch ('\.' + $method + '\s*\(')) { throw "Scaffold does not invoke the selected $method provider." }
    foreach ($other in @('UseSqlServer','UsePostgres','UseInMemory','WithInMemoryStore') | Where-Object { $_ -ne $method }) {
        if ($code -match ('\.' + $other + '\s*\(')) { throw "Scaffold also invokes the unselected $other provider." }
    }
    if ($database -ne 'inmemory') {
        $namedCalls = @([regex]::Matches($code, '\.ConnectionStringName\s*\('))
        if ($namedCalls.Count -ne 1 -or $program.Substring($namedCalls[0].Index) -notmatch
            ('^\.ConnectionStringName\s*\(\s*"' + [regex]::Escape($storeName) + '"\s*\)')) {
            throw "Selected provider does not use the $storeName named connection."
        }
    }
    return [pscustomobject]@{family=$Variant.ShortName; database=$database; providerPackage=$expectedPackage; providerMethod=$method;
        scope='emitted provider, package and configuration selection'; status='passed'}
}

function Test-ScaffoldBuilds {
    Write-Host "`n=== Manifest variant restore/build/test ===" -ForegroundColor Cyan
    if (-not $TemplateVariants.Count) { throw 'No installed template variant roster.' }
    $script:CompletedTemplateVariants = 0
    $usingLocalFeed = -not [string]::IsNullOrWhiteSpace($LocalFeed)
    if ($usingLocalFeed -and ([string]::IsNullOrWhiteSpace($LocalFeedVersion) -or -not (Test-Path -LiteralPath $LocalFeed))) {
        throw '-LocalFeed requires an existing feed and exact -LocalFeedVersion.'
    }
    $buildRoot = Join-Path ([IO.Path]::GetTempPath()) ('xlb-tmpl-' + [Guid]::NewGuid().ToString('N'))
    $script:LastScaffoldRoot = $buildRoot
    [void][IO.Directory]::CreateDirectory($buildRoot)
    $sourceXml = '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
    if ($usingLocalFeed) { $sourceXml = '<add key="candidate" value="' + [Security.SecurityElement]::Escape((Resolve-Path -LiteralPath $LocalFeed).Path) + '" />' + $sourceXml }
    ('<configuration><packageSources><clear />' + $sourceXml + '</packageSources></configuration>') |
        Set-Content -LiteralPath (Join-Path $buildRoot 'nuget.config') -Encoding utf8
    try {
        $index = 0
        foreach ($variant in $TemplateVariants) {
            $variantFailuresBefore = $script:FailCount
            $index++
            $identity = '{0:D4}-{1}' -f $index, $variant.ShortName
            $projectName = 'Consumer_' + $index
            $outputDir = Join-Path $buildRoot $identity
            $evidence = Join-Path $TemplateEvidenceDir $identity
            $newArgs = @('new', $variant.ShortName, '-n', $projectName, '-o', $outputDir) + @($variant.Arguments) + $TemplateHiveArguments
            $newOut = dotnet @newArgs 2>&1 | Out-String
            $newExit = $LASTEXITCODE
            Save-TemplateNativeReceipt $evidence 'new' $newArgs $newExit $newOut
            if ($newExit -ne 0 -or $newOut -notmatch 'was created successfully') {
                Write-TestResult "$identity scaffold" $false "Native exit ${newExit}: $newOut"
                continue
            }
            try {
                $databaseSelection = Assert-TemplateDatabaseSelection $outputDir $variant
                if ($null -ne $databaseSelection) {
                    $databaseSelection | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'database-selection.receipt.json') -Encoding utf8
                }
            } catch {
                Write-TestResult "$identity database selection" $false $_.Exception.Message
                continue
            }
            $projects = @(Get-ChildItem -LiteralPath $outputDir -Recurse -Filter '*.csproj' -File |
                Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' } | Sort-Object FullName)
            if (-not $projects.Count) { Write-TestResult "$identity generated projects" $false 'No project generated.'; continue }
            $projectIndex = 0
            $testProjectCount = 0
            foreach ($project in $projects) {
                $projectIndex++
                $projectEvidence = Join-Path $evidence ('project-' + $projectIndex)
                $versionArgs = @(); if ($usingLocalFeed) { $versionArgs = @("-p:ExcaliburDispatchVersion=$LocalFeedVersion") }
                $restoreArgs = @('restore', $project.FullName, '--configfile', (Join-Path $buildRoot 'nuget.config')) + $versionArgs
                $restoreOut = dotnet @restoreArgs 2>&1 | Out-String
                $restoreExit = $LASTEXITCODE
                Save-TemplateNativeReceipt $projectEvidence 'restore' $restoreArgs $restoreExit $restoreOut
                if ($restoreExit -ne 0) { Write-TestResult "$identity/$projectIndex restore" $false "Native exit ${restoreExit}: $restoreOut"; continue }
                try { Assert-TemplatePackageConsumption $project.FullName $projectEvidence }
                catch { Write-TestResult "$identity/$projectIndex package identity" $false $_.Exception.Message; continue }
                $buildArgs = @('build', $project.FullName, '-c', 'Release', '--no-restore', '--nologo') + $versionArgs
                $buildOut = dotnet @buildArgs 2>&1 | Out-String
                $buildExit = $LASTEXITCODE
                Save-TemplateNativeReceipt $projectEvidence 'build' $buildArgs $buildExit $buildOut
                $errorLines = @($buildOut -split "`r?`n" | Where-Object { $_ -match 'error [A-Z]+\d+' } | Select-Object -Unique)
                $built = ($buildExit -eq 0 -and $errorLines.Count -eq 0)
                $diagnostics = if ($errorLines.Count) { $errorLines } else { @($buildOut -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) }
                if (-not $built) { Write-TestResult "$identity/$projectIndex build" $false ("Native exit ${buildExit}: " + (($diagnostics | Select-Object -First 3) -join ' | ')); continue }
                [xml]$projectXml = Get-Content -LiteralPath $project.FullName -Raw
                # Bounded to the current frozen manifests: explicit test SDK or xunit.v3 executable projects.
                $isTest = @($projectXml.SelectNodes("//*[local-name()='IsTestProject' and text()='true'] | //*[local-name()='PackageReference' and @Include='Microsoft.NET.Test.Sdk']")).Count -gt 0
                $isXunitExecutable = @($projectXml.SelectNodes("//*[local-name()='PackageReference' and @Include='xunit.v3']")).Count -gt 0 -and @($projectXml.SelectNodes("//*[local-name()='OutputType' and text()='Exe']")).Count -gt 0
                $isTest = $isTest -or $isXunitExecutable
                @{project=$project.FullName; isTest=$isTest; classification='current-manifest-XML'; xunitV3Executable=$isXunitExecutable} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectEvidence 'test-classification.json') -Encoding utf8
                if ($isTest) {
                    $testProjectCount++
                    $testArgs = @('test', $project.FullName, '-c', 'Release', '--no-build', '--no-restore', '--logger', 'trx;LogFilePrefix=template', '--results-directory', $projectEvidence, '--blame-hang-timeout', '2m', '--', 'RunConfiguration.TestSessionTimeout=120000')
                    $testOut = dotnet @testArgs 2>&1 | Out-String
                    $testExit = $LASTEXITCODE
                    Save-TemplateNativeReceipt $projectEvidence 'test' $testArgs $testExit $testOut
                    if ($testExit -ne 0) { Write-TestResult "$identity/$projectIndex test" $false "Native exit ${testExit}: $testOut"; continue }
                    $results = @(Get-ChildItem -LiteralPath $projectEvidence -Filter '*.trx')
                    if (-not $results.Count) { Write-TestResult "$identity/$projectIndex test evidence" $false 'No raw TRX produced.'; continue }
                    try { foreach ($result in $results) { Assert-TemplateTrx $result.FullName } }
                    catch { Write-TestResult "$identity/$projectIndex test evidence" $false $_.Exception.Message; continue }
                }
                Write-TestResult "$identity/$projectIndex restore/build$(if ($isTest) {'/test'})" $true
            }
            if ($variant.Parameters.ContainsKey('IncludeTests')) {
                $expectsTests = $variant.Parameters['IncludeTests'] -eq 'true'
                if ($expectsTests -ne ($testProjectCount -gt 0)) {
                    Write-TestResult "$identity generated test selection" $false "IncludeTests=$expectsTests but executable test projects=$testProjectCount"
                }
            }
            @{identity=$identity; parameters=$variant.Parameters; projects=@($projects.FullName);
              status=$(if ($script:FailCount -eq $variantFailuresBefore) {'passed'} else {'failed'})} |
                ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'variant.receipt.json') -Encoding utf8
            if (-not $NoCleanUp -and $script:FailCount -eq $variantFailuresBefore) {
                Remove-TemplateTemporaryDirectory $outputDir
            }
            $script:CompletedTemplateVariants++
        }
    } finally {
        @{candidateSha=$script:TemplateCandidateSha; templatePackageHash=$script:TemplatePackageHash;
          plannedVariants=$TemplateVariants.Count; completedVariants=$script:CompletedTemplateVariants;
          sourceFingerprint=$script:RuntimeSourceIdentity.sha256;
          status='pendingFinalValidation';
          mode=$(if ($LocalFeed) {'candidate'} else {'published-feed'}); producerProvenance='external-P06-manifest-required-for-current-source-pack-claim'} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $TemplateEvidenceDir 'matrix.receipt.json') -Encoding utf8
        if (-not $NoCleanUp -and $script:FailCount -eq 0) {
            Remove-TemplateTemporaryDirectory $buildRoot
        } else { Write-Host "Scaffold evidence preserved at $buildRoot" }
    }
}

function Assert-TemplateTrx {
    param([string]$Path)
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $summaries = @($trx.SelectNodes("//*[local-name()='ResultSummary']"))
    $rows = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($summaries.Count -ne 1 -or $summaries[0].outcome -ne 'Completed') { throw 'Missing or unsuccessful TRX summary.' }
    $counters = @($summaries[0].SelectNodes("*[local-name()='Counters']"))
    if ($counters.Count -ne 1) { throw 'Missing TRX counters.' }
    $counter = $counters[0]
    $total = 0L
    if (-not [long]::TryParse($counter.GetAttribute('total'), [ref]$total) -or $total -le 0) { throw 'Invalid TRX total.' }
    foreach ($name in @('executed','passed')) {
        $count = 0L
        if (-not [long]::TryParse($counter.GetAttribute($name), [ref]$count) -or $count -ne $total) { throw "Incomplete TRX $name counter." }
    }
    foreach ($name in @('failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')) {
        $count = 0L
        if (-not [long]::TryParse($counter.GetAttribute($name), [ref]$count) -or $count -ne 0) { throw "Missing or nonzero TRX $name counter." }
    }
    if ($rows.Count -ne $total -or @($rows | Where-Object {$_.outcome -ne 'Passed'}).Count) { throw 'TRX rows do not match successful counters.' }
}

function Assert-TemplateCleanupTree {
    param([string]$Path)
    # Inspect one level at a time, rejecting links before descending into them.
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($Path)
    while ($pending.Count) {
        $directory = $pending.Pop()
        foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
            if ($entry.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "Unsafe nested cleanup reparse point: $($entry.FullName)" }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
        }
    }
}

function Remove-TemplateTemporaryDirectory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or (Get-Item -LiteralPath $resolved).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe template temporary cleanup path.' }
    Assert-TemplateCleanupTree $resolved
    Remove-Item -LiteralPath $resolved -Recurse -Force
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

$script:TemplateRunCompleted = $false
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
    $script:TemplateRunCompleted = $true
}
catch {
    Write-TestResult 'Template gate execution' $false $_.Exception.ToString()
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
