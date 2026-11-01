$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$testExe=Join-Path $env:TEMP ('OpenMicAI-EdgeRockChecks-'+[guid]::NewGuid().ToString('N')+'.exe')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:EdgeRockChecks',('/out:'+$testExe))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$arguments+='/resource:'+(Join-Path $appRoot 'assets/open-mic-ai-ui.png')+',OpenMicAI.Logo.png'
$arguments+='/resource:'+(Join-Path $appRoot 'assets/open-mic-ai.ico')+',OpenMicAI.Icon.ico'
$arguments+='/resource:'+(Join-Path $appRoot 'edgerock/model-manifest.json')+',OpenMicAI.EdgeRockManifest'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
try {
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'EdgeRockChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'EdgeRock test compilation failed'}
    & $testExe
    if($LASTEXITCODE-ne 0){throw 'EdgeRock tests failed'}
} finally {if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe -Force}}
