param(
 [ValidateSet('ko','en')][string]$Language = 'ko',
 [string]$OutputDirectory = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found. Build on Windows.' }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
& (Join-Path $PSScriptRoot 'scripts\restore.ps1')
$libraries = @('svg\lib\net462\Svg.dll','excss\lib\net48\ExCSS.dll','system.memory\lib\net461\System.Memory.dll','system.buffers\lib\net461\System.Buffers.dll','system.runtime.compilerservices.unsafe\lib\net461\System.Runtime.CompilerServices.Unsafe.dll','system.numerics.vectors\lib\net46\System.Numerics.Vectors.dll')
foreach ($library in $libraries) { Copy-Item -LiteralPath (Join-Path "$PSScriptRoot\vendor" $library) -Destination $outputPath -Force }
$common = @('/nologo', '/reference:System.Web.Extensions.dll', "/resource:$PSScriptRoot\locales\en.json,MiniDeck.English")
if ($Language -eq 'en') { $common += '/define:ENGLISH' }
foreach ($assembly in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')) { $common += '/reference:' + (Join-Path (Split-Path $compiler) "WPF\$assembly") }
$sources = @('MiniDeck.cs','Appearance.cs','LayeredWindow.cs','Onboard.cs','BrowserLink.cs','DialModes.cs','Localization.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$engineResources = Get-ChildItem -LiteralPath "$PSScriptRoot\assets\engines" -Filter '*.svg' | ForEach-Object { "/resource:$($_.FullName),MiniDeck.Engine.$($_.Name)" }
& $compiler @common @engineResources /target:winexe /win32manifest:"$PSScriptRoot\app.manifest" /win32icon:"$PSScriptRoot\assets\minideck.ico" /resource:"$PSScriptRoot\assets\minideck.ico,MiniDeck.AppIcon" /resource:"$PSScriptRoot\assets\minideck.png,MiniDeck.AppImage" /out:"$outputPath\MiniDeck.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:"$outputPath\Svg.dll" @sources
if ($LASTEXITCODE -ne 0) { throw 'MiniDeck build failed' }
& $compiler @common /target:exe /out:"$outputPath\MiniDeck.BrowserHost.exe" "$PSScriptRoot\BrowserHost.cs" "$PSScriptRoot\Localization.cs"
if ($LASTEXITCODE -ne 0) { throw 'Browser host build failed' }
if ($outputPath -ne $PSScriptRoot) {
 Copy-Item -LiteralPath "$PSScriptRoot\fonts" -Destination $outputPath -Recurse -Force
 Copy-Item -LiteralPath "$PSScriptRoot\MiniDeck.exe.config" -Destination $outputPath -Force
}
Write-Output "Built MiniDeck ($Language): $outputPath"
