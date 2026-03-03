$ErrorActionPreference = "Stop"

Write-Host "=== Easy Command (EasyCommand) Build ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw ".NET SDK not found. Install .NET 8 SDK first."
}

dotnet restore "IntentShell/EasyCommand.sln"
dotnet build "IntentShell/EasyCommand.sln" -c Release

Write-Host "Build complete." 
