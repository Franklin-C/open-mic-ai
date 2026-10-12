$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$fixtureRoot=Join-Path $env:TEMP ('OpenMicAI-actions-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'models') -Force | Out-Null
foreach($name in @('tdt-0.6b-v2-q8_0.gguf','Qwen3-4B-Instruct-2507-Q3_K_S.gguf')){New-Item -ItemType HardLink -Path (Join-Path $fixtureRoot ('models\'+$name)) -Value (Join-Path $appRoot ('models\'+$name)) | Out-Null}
Copy-Item -LiteralPath (Join-Path $appRoot 'runtime') -Destination (Join-Path $fixtureRoot 'runtime') -Recurse
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$testExe=Join-Path $fixtureRoot 'TextActionChecks.exe'
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:TextActionChecks',('/out:'+$testExe))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$arguments+='/resource:'+(Join-Path $appRoot 'assets\open-mic-ai-ui.png')+',OpenMicAI.Logo.png'
$arguments+='/resource:'+(Join-Path $appRoot 'assets\open-mic-ai.ico')+',OpenMicAI.Icon.ico'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
& $compiler @arguments @sources (Join-Path $PSScriptRoot 'TextActionChecks.cs') (Join-Path $PSScriptRoot 'PolishChecks.cs')
if($LASTEXITCODE-ne 0){throw 'Text action test compilation failed'}
& $testExe $fixtureRoot (Join-Path $PSScriptRoot 'text-action-checks.json') $appRoot
if($LASTEXITCODE-ne 0){throw ('Text action checks failed; retained fixture: '+$fixtureRoot)}
$resolved=(Resolve-Path -LiteralPath $fixtureRoot).Path
if($resolved-ne [IO.Path]::GetFullPath($fixtureRoot)-or !(Test-Path -LiteralPath (Join-Path $resolved 'TextActionChecks.exe'))){throw 'Fixture cleanup path did not match'}
Remove-Item -LiteralPath $resolved -Recurse -Force
