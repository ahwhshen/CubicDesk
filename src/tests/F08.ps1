$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=Join-Path $root 'work/regressions/F08'
& (Join-Path $root 'build.ps1') -OutputDirectory $out
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$arguments=@('/nologo','/target:exe','/platform:x64','/langversion:5','/utf8output',('/out:'+(Join-Path $out 'F08Checks.exe')),('/reference:'+(Join-Path $out 'CubicDesk.exe')))
foreach($ref in $refs){$arguments+=('/reference:'+(Join-Path $framework $ref))}
$arguments+=(Join-Path $PSScriptRoot 'F08.cs')
& (Join-Path $framework 'csc.exe') $arguments
if($LASTEXITCODE -ne 0){throw 'Regression harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $out 'CubicDesk.exe.config') -Destination (Join-Path $out 'F08Checks.exe.config') -Force
& (Join-Path $out 'F08Checks.exe')
if($LASTEXITCODE -ne 0){throw 'F08 regression checks failed'}
