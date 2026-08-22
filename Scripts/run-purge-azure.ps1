# Purge opérationnelle Azure SQL — conserve catalogue produits + vendeurs + comptes.
# Usage :
#   $env:AZURE_SQL_PASSWORD = "..."
#   .\Scripts\run-purge-azure.ps1

$ErrorActionPreference = "Stop"
if (-not $env:AZURE_SQL_PASSWORD) {
    Write-Error "Definissez `$env:AZURE_SQL_PASSWORD (mot de passe SQL pharmacieadmin)."
}

$root = Split-Path -Parent $PSScriptRoot
Set-Location (Join-Path $root "Pharmacie")

$env:ConnectionStrings__DefaultConnection = "Server=tcp:pharmacie-saintjeanpaul-srv.database.windows.net,1433;Initial Catalog=pharmacie-db;User ID=pharmacieadmin;Password=$($env:AZURE_SQL_PASSWORD);Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

dotnet run --no-launch-profile -- --reset-data
