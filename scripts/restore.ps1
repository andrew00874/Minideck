$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$vendorRoot = Join-Path $projectRoot 'vendor'
New-Item -ItemType Directory -Path $vendorRoot -Force | Out-Null
$packages = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'packages.json') -Raw | ConvertFrom-Json
foreach ($package in $packages) {
 $archive = Join-Path $vendorRoot ($package.id + '.zip')
 if (!(Test-Path -LiteralPath $archive)) {
  $url = 'https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg' -f $package.id,$package.version
  Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
 }
 if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.sha256) { throw "Package hash mismatch: $($package.id)" }
 $destination = Join-Path $vendorRoot $package.id
 if (!(Test-Path -LiteralPath (Join-Path $destination $package.library))) { Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force }
}
