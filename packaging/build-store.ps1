<#
  Builds the Microsoft Store package: packaging\out\Focus-with-a-glance_<version>_x64.msix

  The identity values come from Partner Center (your app → Product management → Product identity):
    .\build-store.ps1 -IdentityName "12345YourName.Focuswithaglance" `
                      -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" `
                      -PublisherDisplayName "Your Name"

  The package is left unsigned; the Store signs it when you upload it.
  Needs makeappx.exe and makepri.exe: from the Windows SDK, or run once with -GetTools to restore
  Microsoft's "Microsoft.Windows.SDK.BuildTools" NuGet package.
#>
param(
  [string]$IdentityName = "FocusWithAGlance.Dev",
  [string]$Publisher = "CN=FocusWithAGlanceDev",
  [string]$PublisherDisplayName = "Focus with a glance",
  [switch]$GetTools
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'FocusGlance\FocusGlance.csproj'
$out = Join-Path $PSScriptRoot 'out'
$layout = Join-Path $out 'layout'

# ---- tools
$BuildToolsVersion = '10.0.26100.1742'
function Find-Tool([string]$name) {
  $kits = 'C:\Program Files (x86)\Windows Kits\10\bin'
  $cands = @()
  if (Test-Path $kits) { $cands += Get-ChildItem $kits -Recurse -Filter $name -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\' } }
  $nuget = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
  if (Test-Path $nuget) { $cands += Get-ChildItem $nuget -Recurse -Filter $name -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\' } }
  ($cands | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}
if ($GetTools -and -not (Find-Tool 'makeappx.exe')) {
  $tmp = Join-Path $out 'tools'
  New-Item -ItemType Directory -Force $tmp | Out-Null
  @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$BuildToolsVersion" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $tmp 'tools.csproj') -Encoding utf8
  dotnet restore (Join-Path $tmp 'tools.csproj')
  if ($LASTEXITCODE) { throw 'Could not restore Microsoft.Windows.SDK.BuildTools' }
}
$makeappx = Find-Tool 'makeappx.exe'
$makepri = Find-Tool 'makepri.exe'
if (-not $makeappx -or -not $makepri) { throw 'makeappx.exe / makepri.exe not found. Install the Windows SDK, or run again with -GetTools.' }

# ---- version from the csproj (Store needs a.b.c.0)
[xml]$x = Get-Content $proj
$ver = ($x.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
$pkgVer = "$ver.0"

# ---- publish (not single-file: faster start inside a package)
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
dotnet publish $proj -c Release -p:PublishSingleFile=false -o $layout
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
Get-ChildItem $layout -Filter '*.xml' | Remove-Item -Force   # API docs from NuGet packages, not needed

# ---- assets + manifest
$assets = Join-Path $PSScriptRoot 'Assets'
if (-not (Test-Path (Join-Path $assets 'StoreLogo.scale-100.png'))) { & (Join-Path $PSScriptRoot 'make-assets.ps1') }
Copy-Item $assets (Join-Path $layout 'Assets') -Recurse -Force
$m = Get-Content (Join-Path $PSScriptRoot 'Package.appxmanifest') -Raw
$m = $m.Replace('$IdentityName$', [Security.SecurityElement]::Escape($IdentityName)).
        Replace('$Publisher$', [Security.SecurityElement]::Escape($Publisher)).
        Replace('$PublisherDisplayName$', [Security.SecurityElement]::Escape($PublisherDisplayName)).
        Replace('$Version$', $pkgVer)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $m, (New-Object Text.UTF8Encoding $false))

# ---- resources.pri (lets Windows pick the right asset scale)
$pricfg = Join-Path $out 'priconfig.xml'
& $makepri createconfig /cf $pricfg /dq en-US_ar-SA /pv 10.0.0 /o | Out-Null
# One combined resources.pri (no split per-scale files: we ship a single .msix, not a bundle)
[xml]$pc = Get-Content $pricfg
$pc.SelectNodes('//packaging') | ForEach-Object { [void]$_.ParentNode.RemoveChild($_) }
$pc.Save($pricfg)
& $makepri new /pr $layout /cf $pricfg /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE) { throw 'makepri failed' }

# ---- pack
$msix = Join-Path $out "Focus-with-a-glance_${pkgVer}_x64.msix"
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE) { throw 'makeappx failed' }
Write-Host "`nStore package: $msix" -ForegroundColor Green
