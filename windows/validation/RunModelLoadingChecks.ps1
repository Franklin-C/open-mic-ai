$ErrorActionPreference='Stop'
$appRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$fixture = Join-Path $env:TEMP ('OpenMicAI-model-ui-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$testExe = Join-Path $fixture 'ModelLoadingChecks.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/optimize+','/main:ModelLoadingChecks',('/out:' + $testExe))
foreach ($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')) { $arguments += '/reference:' + (Join-Path $framework $name) }
$arguments += '/resource:' + (Join-Path $appRoot 'assets\open-mic-ai-ui.png') + ',OpenMicAI.Logo.png'
$arguments += '/resource:' + (Join-Path $appRoot 'assets\open-mic-ai.ico') + ',OpenMicAI.Icon.ico'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments @sources (Join-Path $PSScriptRoot 'ModelLoadingChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Model UI test compilation failed.' }
& $testExe $fixture (Join-Path $PSScriptRoot 'model-loading-checks.json') (Join-Path $appRoot 'models-simplified-preview.png')
if ($LASTEXITCODE -ne 0) { throw 'Model UI checks failed; fixture retained.' }
$resolved = (Resolve-Path -LiteralPath $fixture).Path
if ($resolved -ne [IO.Path]::GetFullPath($fixture) -or !(Test-Path -LiteralPath (Join-Path $resolved 'ModelLoadingChecks.exe'))) { throw 'Fixture cleanup path did not match.' }
Remove-Item -LiteralPath $resolved -Recurse -Force
