param(
  [string]$RepoPath = "C:\Users\Acer\Desktop\Welllplayd"
)

$ErrorActionPreference = "Stop"
$resolvedRepo = (Resolve-Path -LiteralPath $RepoPath).Path
$payloadPath = Join-Path $PSScriptRoot "payload"

if (-not (Test-Path -LiteralPath (Join-Path $resolvedRepo "Ludora.sln"))) {
  throw "RepoPath Welllplayd repo root deyil: $resolvedRepo"
}

Get-ChildItem -LiteralPath $payloadPath -Force | ForEach-Object {
  Copy-Item -LiteralPath $_.FullName -Destination $resolvedRepo -Recurse -Force
}

$duplicateWorkflow = Join-Path $resolvedRepo ".github\workflows\deploy-static-web-app.yml"
if (Test-Path -LiteralPath $duplicateWorkflow) {
  Remove-Item -LiteralPath $duplicateWorkflow -Force
}

Write-Host ""
Write-Host "Azure frontend deploy fix tətbiq edildi." -ForegroundColor Green
Write-Host "Köhnə duplicate workflow silindi." -ForegroundColor Green
Write-Host ""
Write-Host "İndi repo root-da bunları işlət:" -ForegroundColor Cyan
Write-Host "  git add -A"
Write-Host '  git commit -m "Fix Azure frontend deployment"'
Write-Host "  git push origin main"
Write-Host ""
Write-Host "Vacib: tokeni reset etmisənsə GitHub secret-i yenilə:" -ForegroundColor Yellow
Write-Host "AZURE_STATIC_WEB_APPS_API_TOKEN_GREEN_WAVE_0CF533310"
