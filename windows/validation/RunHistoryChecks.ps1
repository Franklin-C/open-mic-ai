param([string]$FixtureRoot = '', [string]$ReportPath = '', [string]$PreviewPath = '')
$ErrorActionPreference = 'Stop'
$appRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (!$FixtureRoot) { $FixtureRoot = Join-Path $env:TEMP ('LocalFlow-history-checks-' + [guid]::NewGuid().ToString('N')) }
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
if (Test-Path -LiteralPath $FixtureRoot) { throw 'Choose a new, empty fixture directory.' }
if (!$ReportPath) { $ReportPath = Join-Path $PSScriptRoot 'history-checks.json' }
if (!$PreviewPath) { $PreviewPath = Join-Path $appRoot 'history-preview.png' }
# Synthetic text only: this check pastes into its own fields and does not record audio.
New-Item -ItemType Directory -Path (Join-Path $FixtureRoot 'models') -Force | Out-Null
foreach ($name in @('tdt-0.6b-v2-q8_0.gguf','Qwen3-4B-Instruct-2507-Q3_K_S.gguf')) {
    New-Item -ItemType HardLink -Path (Join-Path $FixtureRoot ('models\' + $name)) -Value (Join-Path $appRoot ('models\' + $name)) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $appRoot 'runtime') -Destination (Join-Path $FixtureRoot 'runtime') -Recurse
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$testExe = Join-Path $FixtureRoot 'HistoryChecks.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/optimize+','/main:HistoryChecks',('/out:' + $testExe))
foreach ($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')) { $arguments += '/reference:' + (Join-Path $framework $name) }
$arguments += '/resource:' + (Join-Path $appRoot 'assets\open-mic-ai-ui.png') + ',OpenMicAI.Logo.png'
$arguments += '/resource:' + (Join-Path $appRoot 'assets\open-mic-ai.ico') + ',OpenMicAI.Icon.ico'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments @sources (Join-Path $PSScriptRoot 'HistoryChecks.cs') (Join-Path $PSScriptRoot 'FeatureChecks.cs') (Join-Path $PSScriptRoot 'UnifiedChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'History test compilation failed.' }
& $testExe $FixtureRoot $ReportPath $PreviewPath
if ($LASTEXITCODE -ne 0) { throw 'History checks failed; inspect the report. The fixture was retained.' }
# Hard links are regular files, and the entire resolved fixture path is the one created above.
$resolvedFixture = (Resolve-Path -LiteralPath $FixtureRoot).Path
if ($resolvedFixture -ne $FixtureRoot -or !(Test-Path -LiteralPath (Join-Path $resolvedFixture 'HistoryChecks.exe'))) { throw 'Fixture cleanup path did not match.' }
Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
