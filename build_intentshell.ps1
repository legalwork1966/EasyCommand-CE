$ErrorActionPreference = "Stop"

Write-Host "=== Easy Command (IntentShell) Build ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw ".NET SDK not found. Install .NET 8 SDK first."
}

dotnet restore "IntentShell/IntentShell.sln"
dotnet build "IntentShell/IntentShell.sln" -c Release

Write-Host "Build complete." 
