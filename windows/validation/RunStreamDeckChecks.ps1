$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$fixture=Join-Path $env:TEMP ('OpenMicAI-StreamDeck-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$testExe=Join-Path $fixture 'checks.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:StreamDeckChecks',('/out:'+$testExe))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
try {
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'StreamDeckChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'Stream Deck test compilation failed'}
    & $testExe $fixture
    if($LASTEXITCODE-ne 0){throw 'Stream Deck tests failed'}
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    if($resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and (Split-Path $resolved -Leaf).StartsWith('OpenMicAI-StreamDeck-')) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
