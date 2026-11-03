# 用途：读取 Windows 媒体会话、播放进度、封面并执行播放控制。
param([string]$Preferred='', [int]$Interval=800, [string]$RuntimeAssembly='')
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
[Console]::InputEncoding=New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.Drawing
if(-not $RuntimeAssembly){throw 'Cubic Desk runtime assembly is required'}
$null=[Reflection.Assembly]::LoadFrom($RuntimeAssembly)
$null=[Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime]
function Await-Operation($Operation,$ResultType){
  $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
  $task=$method.MakeGenericMethod($ResultType).Invoke($null,@($Operation))
  if(-not $task.Wait(5000)){throw 'SMTC operation timed out'}
  return $task.Result
}
try{
  $manager=Await-Operation ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
  $inputReader=[IO.StreamReader]::new([Console]::OpenStandardInput(),[Text.Encoding]::UTF8)
  $pending=$inputReader.ReadLineAsync()
  $previousTrack=''
  $coverReady=$false
  $nextCoverAttempt=[DateTime]::MinValue
  $coverError=''
  while($true){
    try{
      $sessions=@($manager.GetSessions())
      $session=$null
      if($Preferred){$session=$sessions | Where-Object {$_.SourceAppUserModelId -eq $Preferred} | Select-Object -First 1}
      if(-not $Preferred){$session=$manager.GetCurrentSession()}
      if(-not $Preferred -and -not $session -and $sessions.Count -gt 0){
        $session=$sessions|Where-Object {$_.GetPlaybackInfo().PlaybackStatus.ToString() -eq 'Playing'}|Select-Object -First 1
        if(-not $session){$session=$sessions|Select-Object -First 1}
      }
      if($pending.IsCompleted){
        $command=$pending.Result
        if($null -eq $command){break}
        $pending=$inputReader.ReadLineAsync()
        if($command -eq 'quit'){break}
        if($command.StartsWith('source:')){$Preferred=$command.Substring(7)}
        if($session){
          $op=$null
          switch($command){
            'toggle' {$op=$session.TryTogglePlayPauseAsync()}
            'play' {$op=$session.TryPlayAsync()}
            'pause' {$op=$session.TryPauseAsync()}
            'next' {$op=$session.TrySkipNextAsync()}
            'previous' {$op=$session.TrySkipPreviousAsync()}
          }
          if($op){$null=Await-Operation $op ([bool])}
        }
      }
      $out=@{ok=$true;connected=$false;state='stopped';title='';artist='';album='';position_ms=0;duration_ms=0;sessions=@($sessions|ForEach-Object {$_.SourceAppUserModelId})}
      if($session){
        $props=Await-Operation ($session.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties,Windows.Media.Control,ContentType=WindowsRuntime])
        $timeline=$session.GetTimelineProperties()
        $out.connected=$true
        $out.state=$session.GetPlaybackInfo().PlaybackStatus.ToString().ToLowerInvariant()
        $out.title=$props.Title;$out.artist=$props.Artist;$out.album=$props.AlbumTitle
        $out.position_ms=[math]::Max(0,$timeline.Position.TotalMilliseconds-$timeline.StartTime.TotalMilliseconds)
        $out.duration_ms=[math]::Max(0,$timeline.EndTime.TotalMilliseconds-$timeline.StartTime.TotalMilliseconds)
        $out.source_app=$session.SourceAppUserModelId
        $track=$session.SourceAppUserModelId+'|'+$props.Title+'|'+$props.Artist+'|'+$props.AlbumTitle
        if($track -ne $previousTrack){
          $previousTrack=$track
          $coverReady=$false
          $coverError=''
          $nextCoverAttempt=[DateTime]::MinValue
          $out.cover_b64=''
        }
        if(-not $coverReady -and [DateTime]::UtcNow -ge $nextCoverAttempt){
          $nextCoverAttempt=[DateTime]::UtcNow.AddSeconds(3)
          if($props.Thumbnail){
            try{
              $random=Await-Operation ($props.Thumbnail.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType,Windows.Storage.Streams,ContentType=WindowsRuntime])
              $out.cover_b64=[Convert]::ToBase64String([CubicDesk.SmtcThumbnail]::Jpeg($random))
              $coverReady=$true
              $coverError=''
            }catch{$coverError=$_.Exception.Message}
          }else{
            $coverError='Player thumbnail is not available yet; retrying.'
          }
        }
        $out.cover_error=$coverError
      }
      [Console]::Out.WriteLine(($out|ConvertTo-Json -Compress -Depth 4))
    }catch{[Console]::Out.WriteLine((@{ok=$false;connected=$false;error=$_.Exception.Message;state='error'}|ConvertTo-Json -Compress))}
    Start-Sleep -Milliseconds ([math]::Max(250,$Interval))
  }
}catch{[Console]::Out.WriteLine((@{ok=$false;connected=$false;error=$_.Exception.Message;state='error'}|ConvertTo-Json -Compress));exit 1}
