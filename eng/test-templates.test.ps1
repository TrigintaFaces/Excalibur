# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param([string]$ScriptUnderTest = (Join-Path $PSScriptRoot 'test-templates.ps1'))
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($ScriptUnderTest,[ref]$tokens,[ref]$errors)
if ($errors) { throw 'Template script did not parse.' }
$names=@('Test-ScaffoldBuilds','Assert-TemplateDatabaseSelection','Get-TemplateVariants','Save-TemplateNativeReceipt','Assert-TemplatePackageConsumption','Remove-TemplateTemporaryDirectory','Test-TemplateInstallation','Assert-TemplateTrx','Assert-TemplateCleanupTree','Get-TemplateInputHash')
foreach($name in $names) {
    $functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$true))
    if($functions.Count -ne 1){throw "Expected one production function $name"}
    . ([scriptblock]::Create($functions[0].Extent.Text))
}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('template-exit-control-'+[guid]::NewGuid().ToString('N'))
$fixture=[IO.Path]::GetFullPath($fixture)
$tempParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
if(-not $fixture.StartsWith($tempParent,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture path.'}
[void][IO.Directory]::CreateDirectory($fixture)
$originalPath=$env:PATH
try {
    $inputFile=Join-Path $fixture 'tracked-input.cs'
    $absent=Get-TemplateInputHash $inputFile
    if($absent -cne 'deleted' -or (Get-TemplateInputHash $inputFile) -cne $absent){throw 'Stable tracked deletion was not represented.'}
    Set-Content -LiteralPath $inputFile 'first content'
    $present=Get-TemplateInputHash $inputFile
    if($present -ceq $absent -or $present -cne (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash){throw 'Restoration did not change input identity.'}
    Set-Content -LiteralPath $inputFile 'changed content'
    if((Get-TemplateInputHash $inputFile) -ceq $present){throw 'Content mutation was not detected.'}
    Remove-Item -LiteralPath $inputFile
    if((Get-TemplateInputHash $inputFile) -ceq $present){throw 'Deletion during verification was not detected.'}
    [void][IO.Directory]::CreateDirectory($inputFile)
    $directoryRefused=$false
    try{Get-TemplateInputHash $inputFile}catch{$directoryRefused=$_.Exception.Message -match 'not a file'}
    if(-not $directoryRefused){throw 'Directory replacement was accepted as a deleted file.'}
    Remove-Item -LiteralPath $inputFile
    Write-Host 'PASS: tracked deletion, restoration, byte mutation and directory replacement controls.'
    $worker=Join-Path $fixture 'native-fixture.ps1'
    @'
$stage=$args[0]
Add-Content -LiteralPath $env:TEMPLATE_NATIVE_LOG -Value $stage
Add-Content -LiteralPath ($env:TEMPLATE_NATIVE_LOG+'.argv.jsonl') -Value (ConvertTo-Json -InputObject @($args) -Compress)
if($stage -eq 'pack'){
    $position=[array]::IndexOf($args,'-o')
    $directory=$args[$position+1]
    [void][IO.Directory]::CreateDirectory($directory)
    $package=Join-Path $directory 'Excalibur.Dispatch.Templates.0.1.0.nupkg'
    if(Test-Path -LiteralPath $package){Remove-Item -LiteralPath $package}
    [IO.Compression.ZipFile]::CreateFromDirectory($env:TEMPLATE_NATIVE_PACKAGE_ROOT,$package)
    Write-Output 'packed fixture'
    exit 0
}
if($stage -eq 'new' -and $args[1] -eq 'install'){
    Write-Output ('install argv: '+(ConvertTo-Json -InputObject @($args) -Compress))
    if(-not (Test-Path -LiteralPath $args[2] -PathType Leaf) -or -not $args[2].EndsWith('.nupkg')){exit 23}
    # pwsh -File splits the colon option into two argv entries on Windows.
    $hiveIndex=[array]::IndexOf($args,'--debug:custom-hive')
    if($hiveIndex -lt 0){$hiveIndex=[array]::IndexOf($args,'--debug'); if($hiveIndex -lt 0 -or $args[$hiveIndex+1] -ne 'custom-hive'){exit 23}; $hiveIndex++}
    if($hiveIndex+1 -ge $args.Count -or [string]::IsNullOrWhiteSpace($args[$hiveIndex+1])){exit 23}
    Write-Output 'fixture installed'
    exit 0
}
if($stage -eq 'new' -and $args[1] -eq 'list'){
    Write-Output 'fixture'
    exit 0
}
if($stage -eq 'new'){
    $directory=$args[5]
    [void][IO.Directory]::CreateDirectory($directory)
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include="xunit.v3" /><PackageReference Include="xunit.runner.visualstudio" /></ItemGroup></Project>' |
        Set-Content -LiteralPath (Join-Path $directory 'fixture.csproj')
    Write-Output 'The template was created successfully'
    exit ([int]$env:TEMPLATE_NATIVE_NEW_EXIT)
}
if($stage -eq 'restore'){
    $obj=Join-Path (Split-Path $args[1]) 'obj'
    [void][IO.Directory]::CreateDirectory($obj)
    '{"libraries":{},"packageFolders":{}}' | Set-Content -LiteralPath (Join-Path $obj 'project.assets.json')
    Write-Output 'restore finished'
    exit ([int]$env:TEMPLATE_NATIVE_RESTORE_EXIT)
}
if($stage -eq 'build'){
    if($env:TEMPLATE_NATIVE_BUILD_TEXT -eq 'compiler'){Write-Output 'error CS0001: planted compiler diagnostic'}
    else {Write-Output 'native build process completed'}
    exit ([int]$env:TEMPLATE_NATIVE_BUILD_EXIT)
}
if($stage -eq 'test'){
    $position=[array]::IndexOf($args,'--results-directory')
    $directory=$args[$position+1]
    $summary = '<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary>'
    if ($env:TEMPLATE_NATIVE_TRX -eq 'missing-summary') { $summary = '' }
    if ($env:TEMPLATE_NATIVE_TRX -eq 'aborted') { $summary = $summary.Replace('outcome="Completed"','outcome="Aborted"') }
    if ($env:TEMPLATE_NATIVE_TRX -eq 'partial') { $summary = $summary.Replace('total="1"','total="2"') }
    ('<TestRun><Results><UnitTestResult testName="fixture" outcome="Passed" /></Results>'+ $summary + '</TestRun>') |
        Set-Content -LiteralPath (Join-Path $directory 'fixture.trx')
    Write-Output 'test finished'
    exit ([int]$env:TEMPLATE_NATIVE_TEST_EXIT)
}
throw "Unexpected native stage $stage"
'@ | Set-Content -LiteralPath $worker -Encoding utf8
    $pwsh=(Get-Process -Id $PID).Path
    if($IsWindows){
        $stub=Join-Path $fixture 'dotnet.cmd'
        ('@echo off'+[Environment]::NewLine+'"'+$pwsh+'" -NoProfile -File "%~dp0native-fixture.ps1" %*'+[Environment]::NewLine+'exit /b %ERRORLEVEL%') |
            Set-Content -LiteralPath $stub -Encoding ascii
    } else {
        $stub=Join-Path $fixture 'dotnet'
        ("#!/bin/sh"+[Environment]::NewLine+"exec '$($pwsh.Replace("'","'\''"))' -NoProfile -File '$($worker.Replace("'","'\''"))' "+[char]34+'$@'+[char]34) |
            Set-Content -LiteralPath $stub -Encoding utf8NoBOM
        & chmod +x $stub
        if($LASTEXITCODE -ne 0){throw 'Could not make native fixture executable.'}
    }
    $env:PATH=$fixture+[IO.Path]::PathSeparator+$originalPath
    if((Get-Command dotnet).Source -cne $stub){throw 'Native fixture was not selected.'}
    $TemplateVariants=@([pscustomobject]@{ShortName='fixture';Arguments=@();Parameters=@{}})
    $TemplateHiveArguments=@()
    $TemplateEvidenceDir=Join-Path $fixture 'evidence'
    $script:TemplateCandidateSha='native-control'
    $script:TemplatePackageHash='native-control'
    $script:RuntimeSourceIdentity=@{sha256='native-control'}
    $LocalFeed='';$LocalFeedVersion='';$NoCleanUp=$false
    function Write-TestResult {
        param([string]$TestName,$Passed,[string]$Details='')
        $script:Results.Add(@{Name=$TestName;Passed=[bool]$Passed;Details=$Details})
        if(-not $Passed){$script:FailCount++}
    }
    $controls=@(
        @{Name='healthy';New=0;Restore=0;Build=0;Test=0;Text='clean';Pass=$true;Calls=4},
        @{Name='native-build-failure';New=0;Restore=0;Build=23;Test=0;Text='clean';Pass=$false;Calls=3},
        @{Name='compiler-diagnostic';New=0;Restore=0;Build=0;Test=0;Text='compiler';Pass=$false;Calls=3},
        @{Name='failed-scaffold-success-text';New=23;Restore=0;Build=0;Test=0;Text='clean';Pass=$false;Calls=1},
        @{Name='native-restore-failure';New=0;Restore=23;Build=0;Test=0;Text='clean';Pass=$false;Calls=2},
        @{Name='native-test-failure';New=0;Restore=0;Build=0;Test=23;Text='clean';Pass=$false;Calls=4},
        @{Name='missing-summary';New=0;Restore=0;Build=0;Test=0;Text='clean';Pass=$false;Calls=4;Trx='missing-summary'},
        @{Name='aborted-summary';New=0;Restore=0;Build=0;Test=0;Text='clean';Pass=$false;Calls=4;Trx='aborted'},
        @{Name='partial-summary';New=0;Restore=0;Build=0;Test=0;Text='clean';Pass=$false;Calls=4;Trx='partial'}
    )
    $failed=0
    $databaseControls = Join-Path $fixture 'database-selection'
    [void][IO.Directory]::CreateDirectory($databaseControls)
    foreach ($family in @('excalibur-ddd','excalibur-cqrs','excalibur-saga','excalibur-outbox')) {
        foreach ($database in @('sqlserver','postgresql','inmemory')) {
            $directory = Join-Path $databaseControls ($family + '-' + $database)
            [void][IO.Directory]::CreateDirectory($directory)
            $concern = if ($family -in @('excalibur-ddd','excalibur-cqrs')) {'EventSourcing'} elseif ($family -eq 'excalibur-saga') {'Saga'} else {'Outbox'}
            $key = if ($concern -eq 'EventSourcing') {'EventStore'} else {$concern + 'Store'}
            $suffix = switch ($database) {'sqlserver' {'SqlServer'} 'postgresql' {'Postgres'} 'inmemory' {'InMemory'}}
            $package = if ($concern -eq 'Saga' -and $database -eq 'inmemory') {'Excalibur.Saga'} else {"Excalibur.$concern.$suffix"}
            $method = switch ($database) {'sqlserver' {'UseSqlServer'} 'postgresql' {'UsePostgres'} 'inmemory' {if ($concern -eq 'Saga') {'WithInMemoryStore'} else {'UseInMemory'}}}
            $connections = @{}
            if ($database -ne 'inmemory') {$connections[$key] = if ($database -eq 'postgresql') {'Host=localhost;Database=Fixture;Username=postgres;Password=CHANGE_ME'} else {'Server=localhost;Database=Fixture;Trusted_Connection=True;TrustServerCertificate=True'}}
            @{ConnectionStrings=$connections} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'appsettings.json')
            ('<Project><ItemGroup><PackageReference Include="' + $package + '" /></ItemGroup></Project>') | Set-Content -LiteralPath (Join-Path $directory 'Fixture.csproj')
            $program = if ($database -eq 'inmemory') {"builder.$method();"} else {'builder.' + $method + '(provider => provider.ConnectionStringName("' + $key + '"));'}
            $program | Set-Content -LiteralPath (Join-Path $directory 'Program.cs')
            $variant = [pscustomobject]@{ShortName=$family;Parameters=@{Database=$database}}
            $selection = Assert-TemplateDatabaseSelection $directory $variant
            if ($selection.status -ne 'passed') {throw 'Healthy database fixture refused.'}
        }
    }
    $databaseFixture = Join-Path $databaseControls 'excalibur-outbox-postgresql'
    $databaseVariant = [pscustomobject]@{ShortName='excalibur-outbox';Parameters=@{Database='postgresql'}}
    $negatives = @(
        @{Name='wrong-syntax';File='appsettings.json';Text='{"ConnectionStrings":{"OutboxStore":"Server=localhost;Database=Fixture"}}'},
        @{Name='wrong-key';File='appsettings.json';Text='{"ConnectionStrings":{"EventStore":"Host=localhost;Database=Fixture;Username=postgres;Password=CHANGE_ME"}}'},
        @{Name='duplicate-key';File='appsettings.json';Text='{"ConnectionStrings":{"OutboxStore":"Host=localhost;Database=Fixture;Username=postgres;Password=CHANGE_ME","OutboxStore":"Host=localhost;Database=Fixture;Username=postgres;Password=CHANGE_ME"}}'},
        @{Name='invalid-json';File='appsettings.json';Text='{"ConnectionStrings":{/*comment*/}}'},
        @{Name='missing-package';File='Fixture.csproj';Text='<Project />'},
        @{Name='opposite-package';File='Fixture.csproj';Text='<Project><ItemGroup><PackageReference Include="Excalibur.Outbox.Postgres" /><PackageReference Include="Excalibur.Outbox.SqlServer" /></ItemGroup></Project>'},
        @{Name='comment-only';File='Program.cs';Text='// builder.UsePostgres(provider => provider.ConnectionStringName("OutboxStore"));'},
        @{Name='literal-only';File='Program.cs';Text='var text = ".UsePostgres(";'},
        @{Name='wrong-named-key';File='Program.cs';Text='builder.UsePostgres(provider => provider.ConnectionStringName("EventStore"));'},
        @{Name='opposite-call';File='Program.cs';Text='builder.UseSqlServer(provider => provider.ConnectionStringName("OutboxStore"));'}
    )
    foreach ($negative in $negatives) {
        $path = Join-Path $databaseFixture $negative.File
        $original = [IO.File]::ReadAllBytes($path)
        $refused = $false
        try {
            [IO.File]::WriteAllText($path,$negative.Text)
            try {Assert-TemplateDatabaseSelection $databaseFixture $databaseVariant | Out-Null} catch {$refused=$true}
        } finally {[IO.File]::WriteAllBytes($path,$original)}
        if (-not $refused) {throw "Database observer accepted $($negative.Name)."}
    }
    if ($null -ne (Assert-TemplateDatabaseSelection $databaseFixture ([pscustomobject]@{ShortName='fixture';Parameters=@{}}))) {throw 'Unknown fixture family was not explicitly out of scope.'}
    Write-Host 'PASS: twelve database selection controls and ten negative controls.'
    $env:TEMPLATE_NATIVE_PACKAGE_ROOT=Join-Path $fixture 'package-source'
    $templatePath=Join-Path $env:TEMPLATE_NATIVE_PACKAGE_ROOT 'content/fixture'
    [void][IO.Directory]::CreateDirectory((Join-Path $templatePath '.template.config'))
    '<package><metadata><packageTypes><packageType name="Template" /></packageTypes></metadata></package>' |
        Set-Content -LiteralPath (Join-Path $env:TEMPLATE_NATIVE_PACKAGE_ROOT 'fixture.nuspec')
    '{"shortName":"fixture","symbols":{"Framework":{"type":"parameter","datatype":"choice","choices":[{"choice":"net10.0"}]}}}' |
        Set-Content -LiteralPath (Join-Path $templatePath '.template.config/template.json')
    $Templates=@([pscustomobject]@{ShortName='fixture';TemplatePath=$templatePath})
    $TemplatesDir=$env:TEMPLATE_NATIVE_PACKAGE_ROOT
    $TemplateHiveArguments=@('--debug:custom-hive',(Join-Path $fixture 'hive'))
    $script:Results=[Collections.Generic.List[hashtable]]::new();$script:FailCount=0
    $env:TEMPLATE_NATIVE_LOG=Join-Path $fixture 'artifact-install.calls'
    Test-TemplateInstallation
    $installHealthy=@(Get-Content -LiteralPath $env:TEMPLATE_NATIVE_LOG).Count -eq 3 -and $script:FailCount -eq 0
    $installation=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-TemplateInstallation'},$true))[0].Extent.Text
    $mutant=$installation.Replace("'install', `$packages[0].FullName", "'install', `$TemplatesDir")
    if($mutant -ceq $installation){throw 'Installation mutation matched nothing.'}
    . ([scriptblock]::Create($mutant))
    $env:TEMPLATE_NATIVE_LOG=Join-Path $fixture 'directory-install.calls'
    $refused=$false
    try{Test-TemplateInstallation}catch{$refused=$_.Exception.Message -match 'native exit 23'}
    $installRefusal=$refused -and @(Get-Content -LiteralPath $env:TEMPLATE_NATIVE_LOG).Count -eq 2
    . ([scriptblock]::Create($installation))
    Write-Host "artifact install: $installHealthy; directory mutant refused: $installRefusal"
    if(-not $installHealthy -or -not $installRefusal){$failed++}
    $TemplateVariants=@([pscustomobject]@{ShortName='fixture';Arguments=@();Parameters=@{}})
    $TemplateHiveArguments=@()
    foreach($control in $controls){
        $script:Results=[Collections.Generic.List[hashtable]]::new();$script:FailCount=0
        $env:TEMPLATE_NATIVE_LOG=Join-Path $fixture ($control.Name+'.calls')
        $env:TEMPLATE_NATIVE_NEW_EXIT=[string]$control.New
        $env:TEMPLATE_NATIVE_RESTORE_EXIT=[string]$control.Restore
        $env:TEMPLATE_NATIVE_BUILD_EXIT=[string]$control.Build
        $env:TEMPLATE_NATIVE_TEST_EXIT=[string]$control.Test
        $env:TEMPLATE_NATIVE_BUILD_TEXT=$control.Text
        $env:TEMPLATE_NATIVE_TRX=if($control.ContainsKey('Trx')){$control.Trx}else{'healthy'}
        Test-ScaffoldBuilds
        $calls=@(Get-Content -LiteralPath $env:TEMPLATE_NATIVE_LOG)
        $ok=$script:Results.Count -eq 1 -and $script:Results[0].Passed -eq $control.Pass -and $calls.Count -eq $control.Calls
        if($control.Name -match '^native-.*-failure$'){$ok=$ok -and $script:Results[0].Details -match '23'}
        $exists=Test-Path -LiteralPath $script:LastScaffoldRoot
        $ok=$ok -and ($exists -eq (-not $control.Pass))
        Remove-TemplateTemporaryDirectory $script:LastScaffoldRoot
        Write-Host "$($control.Name): $ok (native calls=$($calls.Count))"
        if(-not $ok){$failed++}
    }
    $templatesRoot=Join-Path $PSScriptRoot '../templates'
    $definitions=@(Get-ChildItem -LiteralPath $templatesRoot -Directory | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName '.template.config/template.json')} |
        ForEach-Object {[pscustomobject]@{TemplatePath=$_.FullName}})
    $roster=@(Get-TemplateVariants $definitions)
    $expected=@{'dispatch-api'=24;'dispatch-worker'=24;'dispatch-minimal-api'=12;'dispatch-serverless'=18;'excalibur-ddd'=12;'excalibur-cqrs'=72;'excalibur-saga'=72;'excalibur-outbox'=72}
    $matrix=$roster.Count -eq 306 -and @($roster | Group-Object ShortName).Count -eq $expected.Count
    foreach($group in @($roster|Group-Object ShortName)){$matrix=$matrix -and $expected.ContainsKey($group.Name) -and $group.Count -eq $expected[$group.Name]}
    $matrix=$matrix -and @($roster|Where-Object {$_.ShortName -eq 'dispatch-api' -and $_.Parameters.Transport -eq 'googlepubsub'}).Count -eq 4
    Write-Host "manifest matrix: $matrix (variants=$($roster.Count))"
    if(-not $matrix){$failed++}
    $install=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-TemplateInstallation'},$true))[0].Extent.Text
    if($install -match 'new install \$templatePath' -or $install -notmatch 'install.*, \$packages\[0\]\.FullName'){throw 'Installation no longer selects the freshly packed artifact.'}
    if($failed){throw "$failed template controls failed."}
    Write-Host 'PASS: native exit controls and manifest matrix.'
    $fixtureCompleted=$true
} finally {
    $env:PATH=$originalPath
    if($fixtureCompleted){Remove-TemplateTemporaryDirectory $fixture}else{Write-Host "Failed fixture retained: $fixture"; Get-ChildItem -LiteralPath $fixture -Recurse -File -Filter "*.log" | ForEach-Object {Get-Content $_.FullName}; Get-ChildItem -LiteralPath $fixture -Filter "*.argv.jsonl" | ForEach-Object {Get-Content $_.FullName}}
}
