param([switch]$RetryOnly)
$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$portable=Join-Path (Split-Path $appRoot) 'OpenMicAI-Portable.exe'
$folder=Join-Path $appRoot 'OpenMicAI.exe'
$owner=Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $portable -or $_.ExecutablePath -eq $folder } | Select-Object -First 1
if (!$owner) { throw 'Run Open Mic AI and load models before these checks.' }
$engine=Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $owner.ProcessId -and $_.Name -eq 'llama-server.exe' } | Select-Object -First 1
if (!$engine) { throw 'The local language model is not running.' }
$port=[regex]::Match($engine.CommandLine,'--port\s+(\d+)').Groups[1].Value
$token=[regex]::Match($engine.CommandLine,'--api-key\s+(\S+)').Groups[1].Value
if (!$port -or !$token) { throw 'Could not identify the app-owned language model.' }
$fixture=Join-Path $env:TEMP ('OpenMicAI-polish-probe-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$connection=Join-Path $fixture 'connection.json'
$testExe=Join-Path $fixture 'PolishProbe.exe'
try {
    @{url=('http://127.0.0.1:'+$port);token=$token} | ConvertTo-Json | Set-Content -LiteralPath $connection -Encoding UTF8
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$framework=Split-Path $compiler
    $arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:PolishProbe',('/out:'+$testExe))
    foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
    $sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'PolishProbe.cs') (Join-Path $PSScriptRoot 'PolishChecks.cs') (Join-Path $PSScriptRoot 'PolishRetryChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'Polish check compilation failed.'}
    if($RetryOnly){& $testExe $connection (Join-Path $PSScriptRoot 'polish-retry-checks.json') 'retry'}else{& $testExe $connection (Join-Path $PSScriptRoot 'polish-checks.json')}
    if($LASTEXITCODE-ne 0){throw 'Polish checks failed; inspect polish-checks.json.'}
} finally {
    $resolved=(Resolve-Path -LiteralPath $fixture).Path
    if($resolved-eq [IO.Path]::GetFullPath($fixture)-and (Test-Path -LiteralPath $connection)){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
