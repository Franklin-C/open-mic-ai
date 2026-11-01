param(
    [string]$Destination=(Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Applications/OpenMicAI'),
    [switch]$NoStart
)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\','/')
$target=[IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
if($source -eq $target -or $target.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Choose an installation folder outside the source checkout.'}
if(!(Test-Path -LiteralPath (Join-Path $source 'OpenMicAI.exe'))){throw 'Build the compiled application with windows/Build.ps1 first.'}
# Preserve a running session: installing requires the source app to be idle and quit.
$running=Get-Process OpenMicAI -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $source 'OpenMicAI.exe') -or $_.Path -eq (Join-Path $target 'OpenMicAI.exe')}
if($running){throw 'Finish the current action and quit Open Mic AI before installing.'}
New-Item -ItemType Directory -Path $target -Force | Out-Null
function Copy-Tree([string]$From,[string]$To,[string[]]$Exclude=@()){
    if(!(Test-Path -LiteralPath $From)){return}
    $options=@($From,$To,'/E','/MT:8','/R:1','/W:1','/NFL','/NDL','/NJH','/NJS','/NP','/XF','*.partial','*.log','setup.lock','setup-progress.json','/XD','*.parts','__pycache__','.cache')
    $options+=$Exclude
    & robocopy @options | Out-Null
    if($LASTEXITCODE -ge 8){throw "Could not copy $From (robocopy exit $LASTEXITCODE)."}
}
Write-Host 'Copying compiled application, installed engines and model library…'
Copy-Item -LiteralPath (Join-Path $source 'OpenMicAI.exe') -Destination (Join-Path $target 'OpenMicAI.exe') -Force
Copy-Tree (Join-Path $source 'models') (Join-Path $target 'models')
foreach($engine in @('speech','text','edgerock','decisions')){
    $exclude=@();if($engine -eq 'decisions'){$exclude+=Join-Path $source 'runtime/decisions/venv'}
    Copy-Tree (Join-Path $source "runtime/$engine") (Join-Path $target "runtime/$engine") $exclude
}
# User files are copied on the first install; updating never overwrites installed preferences/history.
foreach($file in Get-ChildItem -LiteralPath $source -File -Filter '*.json'){
    if($file.Name -in @('model-catalog.json','package-manifest.json')){continue}
    $destinationFile=Join-Path $target $file.Name
    if(!(Test-Path -LiteralPath $destinationFile)){Copy-Item -LiteralPath $file.FullName -Destination $destinationFile}
}
foreach($file in @('THIRD_PARTY_NOTICES.txt','model-catalog.json')){Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file) -Force}
$edgeConfig=Join-Path $target 'runtime/edgerock/setup.json'
if(Test-Path -LiteralPath $edgeConfig){
    $edge=Get-Content -LiteralPath $edgeConfig -Raw | ConvertFrom-Json
    $edge.python=Join-Path $target 'runtime/edgerock/venv/Scripts/python.exe';$edge.model=Join-Path $target 'models/edgerock-4b'
    [IO.File]::WriteAllText($edgeConfig,($edge|ConvertTo-Json))
}
foreach($backend in @('d1-omni-600m','simple-jev-qwen-0.8b')){
    $config=Join-Path $target "runtime/decisions/$backend.json"
    if(Test-Path -LiteralPath $config){$decision=Get-Content -LiteralPath $config -Raw|ConvertFrom-Json;$decision.model=Join-Path $target "models/decisions/$backend";[IO.File]::WriteAllText($config,($decision|ConvertTo-Json))}
}
$shell=New-Object -ComObject WScript.Shell
$programs=Join-Path ([Environment]::GetFolderPath('Programs')) 'Open Mic AI'
New-Item -ItemType Directory -Path $programs -Force | Out-Null
foreach($shortcut in @((Join-Path $programs 'Open Mic AI.lnk'),(Join-Path ([Environment]::GetFolderPath('Desktop')) 'Open Mic AI.lnk'))){
    $link=$shell.CreateShortcut($shortcut);$link.TargetPath=Join-Path $target 'OpenMicAI.exe';$link.WorkingDirectory=$target;$link.IconLocation=(Join-Path $target 'OpenMicAI.exe')+',0';$link.Save()
}
Write-Host "Installed: $target\OpenMicAI.exe"
Write-Host 'Start menu and desktop shortcuts created. Model files and dependencies are independent of the checkout.'
if(!$NoStart){Start-Process -FilePath (Join-Path $target 'OpenMicAI.exe') -WorkingDirectory $target -WindowStyle Normal}
