$ErrorActionPreference = 'Stop'
$extensionId = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'extension-id.txt') -Raw).Trim()
$hostPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'MiniDeck.BrowserHost.exe'
if (!(Test-Path -LiteralPath $hostPath)) { throw 'Build MiniDeck first.' }
$manifestPath = Join-Path $PSScriptRoot 'native-host.json'
@{ name='com.minideck.browser'; description='MiniDeck browser tab activation'; path=$hostPath; type='stdio'; allowed_origins=@("chrome-extension://$extensionId/") } | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding ASCII
$registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.minideck.browser'
New-Item -Path $registryPath -Force | Out-Null
Set-Item -Path $registryPath -Value $manifestPath
Write-Output "Chrome native host registered for extension $extensionId"
