param([string]$LanguageModel='Qwen3-4B-Instruct-2507-Q3_K_S.gguf',[string]$Report='',[switch]$FastWriting,[string]$DecisionModel='auto',[string]$RuntimeRoot='')
$ErrorActionPreference='Stop'
$appRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if(!$Report){$Report=Join-Path $appRoot ('work\inference-'+$LanguageModel+'.json')}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Report))) | Out-Null
$testExe=Join-Path $env:TEMP ('OpenMicAI-InferenceSmoke-'+[guid]::NewGuid().ToString('N')+'.exe')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:InferenceSmokeChecks',('/out:'+$testExe))
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')){$arguments+='/reference:'+(Join-Path $framework $name)}
foreach($resource in @(@('edgerock/server.py','OpenMicAI.EdgeRockServer'),@('edgerock/model-manifest.json','OpenMicAI.EdgeRockManifest'))){$arguments+='/resource:'+(Join-Path $appRoot $resource[0])+','+$resource[1]}
foreach($resource in @(@('decisions/server.py','OpenMicAI.DecisionServer'),@('decisions/model-manifest.json','OpenMicAI.DecisionManifest'))){$arguments+='/resource:'+(Join-Path $appRoot $resource[0])+','+$resource[1]}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
try {
    & $compiler @arguments @sources (Join-Path $PSScriptRoot 'InferenceSmokeChecks.cs') (Join-Path $PSScriptRoot 'PolishChecks.cs')
    if($LASTEXITCODE-ne 0){throw 'Inference smoke compilation failed'}
    & $testExe $(if($RuntimeRoot){$RuntimeRoot}else{$appRoot}) $LanguageModel $Report $(if($FastWriting){'fast'}else{'writing'}) $DecisionModel
    if($LASTEXITCODE-ne 0){throw "Inference smoke failed. Inspect $Report"}
} finally {if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe -Force}}
