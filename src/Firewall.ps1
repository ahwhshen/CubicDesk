param([ValidateRange(1024,65535)][int]$Port,[string]$RemoteAddress,[string]$ProgramPath)
$ErrorActionPreference='Stop'
$result=Join-Path $PSScriptRoot 'data/firewall-result.json'
try{
  $ip=[Net.IPAddress]::Parse($RemoteAddress);$b=$ip.GetAddressBytes()
  if($b.Length -ne 4 -or -not ($b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168))){throw 'A private IPv4 device address is required'}
  $program=(Resolve-Path -LiteralPath $ProgramPath).Path
  if([IO.Path]::GetFileName($program) -ne 'CubicDesk.exe'){throw 'Unexpected application path'}
  $ruleName='CubicDesk.SMTC.'+$Port+'.'+$ip.ToString()
  $existing=Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
  if(-not $existing){New-NetFirewallRule -Name $ruleName -DisplayName ('Cubic Desk SMTC '+$Port+' / '+$ip) -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -RemoteAddress $ip.ToString() -Program $program -Profile Any|Out-Null}
  @{ok=$true;port=$Port;remote=$ip.ToString();rule=$ruleName}|ConvertTo-Json|Set-Content -LiteralPath $result -Encoding UTF8
}catch{@{ok=$false;error=$_.Exception.Message}|ConvertTo-Json|Set-Content -LiteralPath $result -Encoding UTF8;exit 1}
