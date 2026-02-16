# MacMode - Publish Script
# Produces a self-contained single-file exe for Windows x64.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "./dist"
)

$ErrorActionPreference = "Stop"

Write-Host "Building MacMode..." -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime:       $Runtime"
Write-Host "  Output:        $OutputDir"
Write-Host ""

# Clean output directory
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Publish
dotnet publish src/MacMode.App/MacMode.App.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy profiles and settings to output
Copy-Item -Path "profiles" -Destination "$OutputDir/profiles" -Recurse -Force
Copy-Item -Path "settings.json" -Destination "$OutputDir/settings.json" -Force

Write-Host ""
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host "Output: $OutputDir"
Write-Host ""
Write-Host "Contents:" -ForegroundColor Yellow
Get-ChildItem $OutputDir -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring((Resolve-Path $OutputDir).Path.Length + 1)
    $size = if ($_.PSIsContainer) { "[DIR]" } else { "{0:N0} KB" -f ($_.Length / 1KB) }
    Write-Host "  $relativePath  ($size)"
}
