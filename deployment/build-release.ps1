param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$RootDirectory = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RootDirectory "deployment-artifacts"
}

if ([string]::IsNullOrWhiteSpace($env:VITE_API_BASE_URL)) {
    throw "VITE_API_BASE_URL environment variable must be set before building."
}
if ([string]::IsNullOrWhiteSpace($env:VITE_API_ORIGIN)) {
    throw "VITE_API_ORIGIN environment variable must be set before building."
}

$ApiProject = Join-Path $RootDirectory "presentation/Ludora.API/Ludora.API.csproj"
$FrontendDirectory = Join-Path $RootDirectory "presentation/Frontend/ludora-client"
$ApiOutput = Join-Path $OutputDirectory "api"
$FrontendOutput = Join-Path $OutputDirectory "frontend"

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}
New-Item $ApiOutput -ItemType Directory -Force | Out-Null
New-Item $FrontendOutput -ItemType Directory -Force | Out-Null

dotnet restore $ApiProject
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet publish $ApiProject --configuration Release --no-restore --output $ApiOutput /p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Push-Location $FrontendDirectory
try {
    npm ci --include=optional
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }

    npm run verify
    if ($LASTEXITCODE -ne 0) { throw "Frontend verification failed." }

    npm run audit:prod
    if ($LASTEXITCODE -ne 0) { throw "Frontend production dependency audit failed." }

    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }

    Copy-Item (Join-Path $FrontendDirectory "dist/*") $FrontendOutput -Recurse -Force
}
finally {
    Pop-Location
}

Write-Host "Deployment artifacts created in: $OutputDirectory"
