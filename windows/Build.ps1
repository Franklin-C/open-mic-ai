param([string]$EmbeddedRuntime = '', [string]$Output = '')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
if (!$Output) { $Output = Join-Path $PSScriptRoot 'OpenMicAI.exe' }
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', ('/out:' + $Output))
$arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'assets/open-mic-ai.ico')
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets/open-mic-ai.ico') + ',OpenMicAI.Icon.ico'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets/open-mic-ai-ui.png') + ',OpenMicAI.Logo.png'
if ($EmbeddedRuntime) { $arguments += '/resource:' + $EmbeddedRuntime + ',LocalFlow.Runtime.zip' }
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += '/reference:' + (Join-Path $framework 'WPF\UIAutomationClient.dll')
$arguments += '/reference:' + (Join-Path $framework 'WPF\UIAutomationTypes.dll')
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Get-Item -LiteralPath $Output | Select-Object Name, Length
