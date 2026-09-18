param([ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$')][string]$Version='0.2.0-beta.1')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$distRoot = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
# A new staging folder prevents stale build/test/private files entering a ZIP.
$stageRoot = Join-Path $projectRoot ('artifacts\package-' + [Guid]::NewGuid().ToString('N'))
foreach ($language in @('ko','en')) {
 $name = "MiniDeck-$Version-win-$language"
 $folder = Join-Path $stageRoot $name
 & (Join-Path $projectRoot 'build.ps1') -Language $language -OutputDirectory $folder
 Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $folder -Recurse
 Copy-Item -LiteralPath (Join-Path $projectRoot 'sample-icons') -Destination $folder -Recurse
 Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $folder -Recurse
 foreach ($doc in @('README.md','README.ko.md','THIRD-PARTY.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $doc) -Destination $folder }
 if($language -eq 'ko') { Copy-Item -LiteralPath (Join-Path $projectRoot 'README.ko.md') -Destination (Join-Path $folder 'START-HERE.md') }
 else { Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $folder 'START-HERE.md') }
 $extensionFolder = Join-Path $folder 'browser-extension'
 New-Item -ItemType Directory -Path $extensionFolder -Force | Out-Null
 foreach ($file in @('background.js','manifest.json','extension-id.txt','setup.ps1','README.md','README.ko.md')) { Copy-Item -LiteralPath (Join-Path "$projectRoot\browser-extension" $file) -Destination $extensionFolder }
 Copy-Item -LiteralPath "$projectRoot\browser-extension\_locales" -Destination $extensionFolder -Recurse
 Compress-Archive -LiteralPath $folder -DestinationPath (Join-Path $distRoot "$name.zip") -Force
}
$hashes = foreach ($language in @('ko','en')) {
 $zip = Join-Path $distRoot "MiniDeck-$Version-win-$language.zip"
 '{0}  {1}' -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($zip)
}
$hashes | Set-Content -LiteralPath (Join-Path $distRoot 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release packages: $distRoot"
