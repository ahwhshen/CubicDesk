# 用途：生成干净的源码包、Windows 便携包与 SHA-256 校验文件。
param(
  [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$')][string]$Version='1.5.1',
  [string]$MediaToolsPath=''
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(!$MediaToolsPath){$MediaToolsPath=Join-Path $root 'outputs/CubicDesk/tools/ffmpeg'}
$media=[IO.Path]::GetFullPath($MediaToolsPath)
$metadataPath=Join-Path $media 'component.json'
if(!(Test-Path -LiteralPath $metadataPath)){throw 'Missing tools/ffmpeg/component.json. Copy the complete tools/ffmpeg folder from the Windows portable release, or supply -MediaToolsPath.'}
$metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($name in @('ffmpeg.exe','ffprobe.exe')){
  $file=Join-Path $media $name
  $expected=$metadata.files.$name
  if(!$expected -or !(Test-Path -LiteralPath $file -PathType Leaf)){throw ('Missing media component: '+$name)}
  if((Get-Item -LiteralPath $file).Length -ne $expected.size -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected.sha256){throw ('Media component hash mismatch: '+$name)}
}
foreach($name in @('LICENSE','README.txt')){if(!(Test-Path -LiteralPath (Join-Path $media $name))){throw ('Missing media notice: '+$name)}}
$temporary=Join-Path $root ('work/release-'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $temporary 'CubicDesk-source'
$portable=Join-Path $temporary 'CubicDesk'
$out=Join-Path $root 'outputs/releases'
New-Item -ItemType Directory -Path $source,$portable,$out -Force | Out-Null
$rootFiles=@('README.md','LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md','.gitignore','.gitattributes','build.ps1','package.ps1')
foreach($name in $rootFiles){Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $source $name)}
$srcRoot=Join-Path $root 'src'
$sourceFiles=@(Get-ChildItem -LiteralPath $srcRoot -Recurse -Force -File | Where-Object {
  $relative=$_.FullName.Substring($srcRoot.Length+1).Replace('\','/')
  $relative -notlike 'device-web/*' -and $relative -notlike 'assets/app-icons/*.png' -and $_.Extension -in @('.cs','.ps1','.json','.md','.xaml','.config','.ico')
})
foreach($file in $sourceFiles){$relative=$file.FullName.Substring($root.Length+1);$destination=Join-Path $source $relative;New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $destination}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $source -Recurse
# Build from the clean source tree so local assets and user data cannot leak into a public release.
& (Join-Path $source 'build.ps1') -OutputDirectory $portable
New-Item -ItemType Directory -Path (Join-Path $portable 'tools/ffmpeg') -Force | Out-Null
foreach($name in @('ffmpeg.exe','ffprobe.exe','LICENSE','README.txt','component.json')){Copy-Item -LiteralPath (Join-Path $media $name) -Destination (Join-Path $portable 'tools/ffmpeg')}
foreach($name in @('LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $portable}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $portable -Recurse
Add-Type -AssemblyName System.IO.Compression.FileSystem
$portableZip=Join-Path $out ('CubicDesk-v'+$Version+'-windows-x64.zip')
$sourceZip=Join-Path $out ('CubicDesk-v'+$Version+'-source.zip')
foreach($file in @($portableZip,$sourceZip)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
[IO.Compression.ZipFile]::CreateFromDirectory($portable,$portableZip,[IO.Compression.CompressionLevel]::Optimal,$true)
[IO.Compression.ZipFile]::CreateFromDirectory($source,$sourceZip,[IO.Compression.CompressionLevel]::Optimal,$true)
foreach($file in @($portableZip,$sourceZip)){
  $archive=[IO.Compression.ZipFile]::OpenRead($file)
  try{
    foreach($entry in $archive.Entries){
      if($entry.FullName -match '(^|/)(data|work|\.git)(/|$)' -or $entry.FullName -match '(^|/)AGENTS\.md$' -or $entry.FullName -like '*/device-web/*' -or $entry.FullName -like '*/assets/app-icons/*.png'){throw ('Unexpected private or local-only release entry: '+$entry.FullName)}
      if($entry.Length -gt 0){$stream=$entry.Open();try{$stream.CopyTo([IO.Stream]::Null)}finally{$stream.Dispose()}}
    }
  }finally{$archive.Dispose()}
}
$sums=foreach($file in @($portableZip,$sourceZip)){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)}
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),$sums,(New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{Version=$Version;SourceFiles=$sourceFiles.Count;Portable=$portableZip;Source=$sourceZip;Checksums=(Join-Path $out 'SHA256SUMS.txt');CleanSource=$source;CleanRuntime=$portable} | ConvertTo-Json
