param(
 [ValidateSet('Quality','Faster')][string]$ModelSize='Quality',
 [ValidatePattern('^Vulkan[0-9]+$')][string]$GpuDevice='Vulkan0'
)
$ErrorActionPreference='Stop'
if(Get-Process -Name MiniDeck -ErrorAction SilentlyContinue){throw 'Close MiniDeck before running local translation setup.'}
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$installRoot=Join-Path $env:LOCALAPPDATA 'MiniDeck\translation'
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
function Download-Verified([string]$Url,[string]$Name,[string]$Hash) {
 $destination=Join-Path $installRoot $Name
 if((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $Hash){return $destination}
 $partial=$destination+'.partial'
 Write-Output "Downloading $Name" | Out-Host
 Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $partial
 if((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $Hash){throw "SHA-256 verification failed: $Name"}
 Move-Item -LiteralPath $partial -Destination $destination -Force
 return $destination
}
$archive=Download-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b11960/llama-b11960-bin-win-vulkan-x64.zip' 'llama-b11960-bin-win-vulkan-x64.zip' 'bac6914b5e83523eb008c6789eaa12669f93fbc196c3b9e8237b6672323b33e9'
if($ModelSize -eq 'Quality') {
 $modelId='Hy-MT2-7B'
 $modelPath=Download-Verified 'https://huggingface.co/tencent/Hy-MT2-7B-GGUF/resolve/ab8472660ac61fac25f1af43fac2599d52a8a775/Hy-MT2-7B-Q4_K_M.gguf' 'Hy-MT2-7B-Q4_K_M.gguf' '9f96256500f3fc1ab4d64336b58f52a949a95ad7516b0c229476eef782f9f77b'
} else {
 $modelId='Hy-MT2-1.8B'
 $modelPath=Download-Verified 'https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF/resolve/a0c709d9fac510f2c807aa3af52872340dc37a4a/Hy-MT2-1.8B-Q8_0.gguf' 'Hy-MT2-1.8B-Q8_0.gguf' '5c3fe0b1408a5ceb0143184ef247b11b579c525f4b02b060e6c851bb76fef1a4'
}
$runtimeRoot=Join-Path $installRoot 'llama-b11960'
Expand-Archive -LiteralPath $archive -DestinationPath $runtimeRoot -Force
$utf8=New-Object Text.UTF8Encoding($false)
$keyFile=Join-Path $installRoot 'api-key.txt'
if(!(Test-Path -LiteralPath $keyFile)) {
 $bytes=New-Object byte[] 32
 $random=[Security.Cryptography.RandomNumberGenerator]::Create()
 try {$random.GetBytes($bytes)} finally {$random.Dispose()}
 [IO.File]::WriteAllText($keyFile,([BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant()),$utf8)
}
$baseUrl='http://127.0.0.1:17865'
$manifest=@{Executable=(Join-Path $runtimeRoot 'llama-server.exe');ModelPath=$modelPath;ModelId=$modelId;BaseUrl=$baseUrl;ApiKeyFile=$keyFile;GpuDevice=$GpuDevice}
[IO.File]::WriteAllText((Join-Path $installRoot 'runtime.json'),($manifest | ConvertTo-Json),$utf8)
foreach($license in @(@{Repository='tencent/Hy-MT2-7B-GGUF';Path='LICENSE.txt';Name='MODEL-LICENSE.txt'},@{Repository='ggml-org/llama.cpp';Path='LICENSE';Name='RUNTIME-LICENSE.txt'})) {
 $url=if($license.Name -eq 'MODEL-LICENSE.txt'){'https://huggingface.co/'+$license.Repository+'/resolve/ab8472660ac61fac25f1af43fac2599d52a8a775/'+$license.Path}else{'https://raw.githubusercontent.com/'+$license.Repository+'/b11960/'+$license.Path}
 Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile (Join-Path $installRoot $license.Name)
}
$settingsFile=Join-Path $env:APPDATA 'MiniDeck\settings.json'
if(Test-Path -LiteralPath $settingsFile) {
 $settings=Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
 $local=@{Enabled=$true;Provider='OpenAI';BaseUrl=$baseUrl;Model=$modelId}
 if($settings.PSObject.Properties['LocalTranslation'] -and $settings.LocalTranslation.TargetLanguage){$local.TargetLanguage=$settings.LocalTranslation.TargetLanguage}
 $settings | Add-Member -NotePropertyName LocalTranslation -NotePropertyValue $local -Force
 Copy-Item -LiteralPath $settingsFile -Destination ($settingsFile+'.pre-local-translation-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.bak')
 $temporary=$settingsFile+'.tmp'
 [IO.File]::WriteAllText($temporary,($settings | ConvertTo-Json -Depth 20),$utf8)
 [IO.File]::Replace($temporary,$settingsFile,$settingsFile+'.bak')
}
Write-Output "Ready: $modelId. MiniDeck starts the local GPU server on the first translation and stops its own server when it exits."
