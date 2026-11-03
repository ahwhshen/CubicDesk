param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$out=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root 'outputs/CubicDesk'}
New-Item -ItemType Directory -Path $out -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$argsList=@('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5','/utf8output',('/out:'+(Join-Path $out 'CubicDesk.exe')))
foreach($ref in $refs){$argsList+=('/reference:'+(Join-Path $framework $ref))}
if(Test-Path (Join-Path $root 'src/app.ico')){$argsList+=('/win32icon:'+(Join-Path $root 'src/app.ico'))}
$argsList+=Get-ChildItem (Join-Path $root 'src/*.cs')|ForEach-Object {$_.FullName}
& (Join-Path $framework 'csc.exe') $argsList
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Copy-Item (Join-Path $root 'src/*.ps1') -Destination $out -Force
Copy-Item (Join-Path $root 'src/Theme.xaml') -Destination $out -Force
Copy-Item (Join-Path $root 'src/assets') -Destination $out -Recurse -Force
Copy-Item (Join-Path $root 'src/dependencies.json') -Destination $out -Force
Copy-Item (Join-Path $root 'src/依赖清单格式.md') -Destination $out -Force
Copy-Item (Join-Path $root 'src/第三方组件说明.md') -Destination $out -Force
Copy-Item (Join-Path $root 'src/CubicDesk.exe.config') -Destination $out -Force
if(Test-Path (Join-Path $root 'src/app.ico')){Copy-Item (Join-Path $root 'src/app.ico') -Destination $out -Force}
if(Test-Path (Join-Path $root 'README.md')){Copy-Item (Join-Path $root 'README.md') (Join-Path $out '使用说明.md') -Force}
Write-Host ('Built: '+(Join-Path $out 'CubicDesk.exe'))
