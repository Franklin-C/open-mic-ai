$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:Tests',('/out:'+(Join-Path $PSScriptRoot 'Verify.exe')))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$arguments+='/resource:'+(Join-Path $appRoot 'assets\open-mic-ai-ui.png')+',OpenMicAI.Logo.png'
$arguments+='/resource:'+(Join-Path $appRoot 'assets\open-mic-ai.ico')+',OpenMicAI.Icon.ico'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
& $compiler @arguments @sources (Join-Path $PSScriptRoot 'NativeTests.cs') (Join-Path $PSScriptRoot 'LibraryChecks.cs')
if($LASTEXITCODE-ne 0){throw 'Test compilation failed'}
# Quit Open Mic AI first so its global shortcut is available for the verification.
# This test opens its own fields and briefly captures microphone PCM, which it discards.
& (Join-Path $PSScriptRoot 'Verify.exe') $appRoot (Join-Path $PSScriptRoot 'verification-result.json') 'Qwen3-4B-Instruct-2507-Q3_K_S.gguf'
if($LASTEXITCODE-ne 0){throw 'A verification check failed; inspect verification-result.json.'}
