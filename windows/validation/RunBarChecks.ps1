$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$fixture=Join-Path $env:TEMP ('OpenMicAI-Bar-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$testExe=Join-Path $fixture 'checks.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:BarChecks',('/out:'+$testExe))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$arguments+='/resource:'+(Join-Path $appRoot 'assets/open-mic-ai-ui.png')+',OpenMicAI.Logo.png'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
try {
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'BarChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'Dictation bar test compilation failed'}
    & $testExe $fixture @args
    if($LASTEXITCODE-ne 0){throw 'Dictation bar tests failed'}
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    if($resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and (Split-Path $resolved -Leaf).StartsWith('OpenMicAI-Bar-')) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
