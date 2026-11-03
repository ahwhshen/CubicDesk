param([ValidateSet('Status','Start','Stop','Configure','Credentials')][string]$Action='Status')
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
[Console]::InputEncoding=New-Object Text.UTF8Encoding($false)
$null=[Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]
$null=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
Add-Type -AssemblyName System.Runtime.WindowsRuntime
function Await-Operation($Operation,$ResultType){
  $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
  $task=$method.MakeGenericMethod($ResultType).Invoke($null,@($Operation))
  if(-not $task.Wait(25000)){throw 'Windows operation timed out'}
  return $task.Result
}
function Await-Action($Operation){
  $task=[System.WindowsRuntimeSystemExtensions]::AsTask($Operation)
  if(-not $task.Wait(25000)){throw 'Windows operation timed out'}
  $task.GetAwaiter().GetResult()
}
try{
  $inputData=@{}
  if($Action -eq 'Configure' -or $Action -eq 'Start' -or $Action -eq 'Credentials'){
    $line=[Console]::In.ReadLine()
    if($line){$inputData=$line|ConvertFrom-Json}
  }
  $profiles=@([Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles() | Where-Object {$_.GetNetworkConnectivityLevel().ToString() -eq 'InternetAccess'})
  $allProfiles=@([Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles())
  $profile=$null
  if($inputData.profile){$profile=$allProfiles | Where-Object {$_.ProfileName -eq $inputData.profile} | Select-Object -First 1}
  if(-not $profile){$profile=$profiles | Where-Object {$_.NetworkAdapter.IanaInterfaceType -eq 6} | Select-Object -First 1}
  if(-not $profile){$profile=$profiles | Select-Object -First 1}
  if(-not $profile){$profile=$allProfiles | Select-Object -First 1}
  if(-not $profile){throw 'No usable connection profile'}
  $manager=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($profile)
  $operationType=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
  $config=$manager.GetCurrentAccessPointConfiguration()
  if($Action -eq 'Configure'){
    if(-not $inputData.ssid -or [Text.Encoding]::UTF8.GetByteCount($inputData.ssid) -gt 32){throw 'SSID must be 1-32 UTF-8 bytes'}
    if($inputData.password.Length -lt 8 -or $inputData.password.Length -gt 63){throw 'Password must have 8-63 characters'}
    if($manager.TetheringOperationalState.ToString() -eq 'On'){
      $stopped=Await-Operation ($manager.StopTetheringAsync()) $operationType
      if($stopped.Status.ToString() -ne 'Success'){throw $stopped.Status.ToString()}
    }
    $config.Ssid=$inputData.ssid
    $config.Passphrase=$inputData.password
    $config.Band=[Windows.Networking.NetworkOperators.TetheringWiFiBand]::TwoPointFourGigahertz
    Await-Action ($manager.ConfigureAccessPointAsync($config))
    if($inputData.restart){
      $started=Await-Operation ($manager.StartTetheringAsync()) $operationType
      if($started.Status.ToString() -ne 'Success'){throw $started.Status.ToString()}
    }
  }
  if($Action -eq 'Start' -and $manager.TetheringOperationalState.ToString() -ne 'On'){
    $result=Await-Operation ($manager.StartTetheringAsync()) $operationType
    if($result.Status.ToString() -ne 'Success'){throw ($result.Status.ToString()+' '+$result.AdditionalErrorMessage)}
  }
  if($Action -eq 'Stop' -and $manager.TetheringOperationalState.ToString() -ne 'Off'){
    $result=Await-Operation ($manager.StopTetheringAsync()) $operationType
    if($result.Status.ToString() -ne 'Success'){throw $result.Status.ToString()}
  }
  $config=$manager.GetCurrentAccessPointConfiguration()
  $out=@{ok=$true;profile=$profile.ProfileName;state=$manager.TetheringOperationalState.ToString();ssid=$config.Ssid;band=$config.Band.ToString();clients=$manager.ClientCount;profiles=@($profiles | ForEach-Object {$_.ProfileName});peers=@($manager.GetTetheringClients() | ForEach-Object {@{mac=$_.MacAddress;hosts=@($_.HostNames | ForEach-Object {$_.CanonicalName})}})}
  if($Action -eq 'Credentials'){$out.password=$config.Passphrase}
  $out|ConvertTo-Json -Depth 6 -Compress
}catch{
  @{ok=$false;error=$_.Exception.Message}|ConvertTo-Json -Compress
  exit 1
}
