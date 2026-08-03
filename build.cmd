@echo off
rem The repository's build entry point on Windows. CI runs this; contributors run this.
rem
rem   build.cmd                                    restore + build
rem   build.cmd -Test
rem   build.cmd -Test -Project eng\ci\shards\UnitTests-Core.slnf
rem   build.cmd -Pack
rem
rem Thin by design: the logic lives in eng\build.ps1 so this and build.sh cannot drift.

where pwsh >nul 2>&1
if errorlevel 1 (
    echo pwsh ^(PowerShell 7+^) is required: https://learn.microsoft.com/powershell/scripting/install/installing-powershell 1>&2
    exit /b 2
)

pwsh -NoProfile -NonInteractive -File "%~dp0eng\build.ps1" %*
exit /b %ERRORLEVEL%
