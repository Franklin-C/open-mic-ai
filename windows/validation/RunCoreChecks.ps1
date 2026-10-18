$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$testExe=Join-Path $env:TEMP ('OpenMicAI-CoreChecks-'+[guid]::NewGuid().ToString('N')+'.exe')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:CoreChecks',('/out:'+$testExe))
$arguments+='/resource:'+(Join-Path $PSScriptRoot 'fixtures/tone-aac.base64')+',OpenMicAI.MediaTone'
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
try {
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'CoreChecks.cs') (Join-Path $PSScriptRoot 'PolishRetryChecks.cs') (Join-Path $PSScriptRoot 'MediaChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'Core test compilation failed'}
    & $testExe
    if($LASTEXITCODE-ne 0){throw 'Core tests failed'}
} finally { if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe -Force} }
