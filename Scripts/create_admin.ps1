# Script creation compte Administrateur
# A executer UNE SEULE FOIS

param(
    [string]$Email,
    [string]$DisplayName = "Administrateur"
)

function Escape-Sql([string]$value) {
    if ($null -eq $value) { return "" }
    return $value.Replace("'", "''")
}

if (-not $Email) {
    $Email = Read-Host "Email administrateur"
}

$Password = Read-Host "Mot de passe" -AsSecureString
$PasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))

$PasswordConfirm = Read-Host "Confirmer le mot de passe" -AsSecureString
$PasswordConfirmPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PasswordConfirm))

if ($PasswordPlain -ne $PasswordConfirmPlain) {
    Write-Host "Les mots de passe ne correspondent pas." -ForegroundColor Red
    exit 1
}

$AzurePassword = Read-Host "Mot de passe Azure SQL" -AsSecureString
$AzurePlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AzurePassword))

$UserId = [System.Guid]::NewGuid().ToString()
$SecurityStamp = [System.Guid]::NewGuid().ToString("N")
$ConcurrencyStamp = [System.Guid]::NewGuid().ToString()
$NormalizedEmail = $Email.ToUpperInvariant()

Write-Host "Creation du compte..." -ForegroundColor Cyan

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "admin_create"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

$ProgramCs = @"
using Microsoft.AspNetCore.Identity;

var hasher = new PasswordHasher<object>();
var hash = hasher.HashPassword(new object(), args[0]);
Console.WriteLine(hash);
"@

$ProjectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Identity.Core" Version="9.0.1" />
  </ItemGroup>
</Project>
"@

Set-Content -Path (Join-Path $TempDir "Program.cs") -Value $ProgramCs -Encoding UTF8
Set-Content -Path (Join-Path $TempDir "hash.csproj") -Value $ProjectXml -Encoding UTF8

$HashOutput = & dotnet run --project (Join-Path $TempDir "hash.csproj") -- $PasswordPlain
$Hash = ($HashOutput | Select-Object -Last 1).Trim()

Remove-Item $TempDir -Recurse -Force

if (-not $Hash -or $Hash.Length -lt 20) {
    Write-Host "Erreur lors du hashage." -ForegroundColor Red
    exit 1
}

$EmailSql = Escape-Sql $Email
$NormalizedSql = Escape-Sql $NormalizedEmail
$DisplaySql = Escape-Sql $DisplayName
$HashSql = Escape-Sql $Hash

$Sql = @"
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM AspNetUsers WHERE NormalizedEmail = N'$NormalizedSql' OR NormalizedUserName = N'$NormalizedSql')
BEGIN
    ROLLBACK;
    RAISERROR('Un utilisateur avec cet email existe deja.', 16, 1);
    RETURN;
END

INSERT INTO AspNetUsers (
    Id, UserName, NormalizedUserName,
    Email, NormalizedEmail,
    EmailConfirmed, PasswordHash,
    SecurityStamp, ConcurrencyStamp,
    PhoneNumberConfirmed,
    TwoFactorEnabled, LockoutEnabled,
    AccessFailedCount, DisplayName
) VALUES (
    N'$UserId',
    N'$EmailSql',
    N'$NormalizedSql',
    N'$EmailSql',
    N'$NormalizedSql',
    1,
    N'$HashSql',
    N'$SecurityStamp',
    N'$ConcurrencyStamp',
    0, 0, 1, 0,
    N'$DisplaySql'
);

DECLARE @RoleId NVARCHAR(450);
SELECT @RoleId = Id FROM AspNetRoles WHERE NormalizedName = N'ADMINISTRATEUR';
IF @RoleId IS NOT NULL
    INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (N'$UserId', @RoleId);

DECLARE @TitulaireId NVARCHAR(450);
SELECT @TitulaireId = Id FROM AspNetRoles WHERE NormalizedName = N'PHARMACIENTITULAIRE';
IF @TitulaireId IS NOT NULL
    INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (N'$UserId', @TitulaireId);

COMMIT;
PRINT 'OK: compte admin cree';
"@

$SqlFile = [System.IO.Path]::GetTempFileName() + ".sql"
Set-Content -Path $SqlFile -Value $Sql -Encoding UTF8

sqlcmd `
    -S "tcp:pharmacie-saintjeanpaul-srv.database.windows.net,1433" `
    -d pharmacie-db `
    -U pharmacieadmin `
    -P $AzurePlain `
    -i $SqlFile `
    -C -I

$SqlExit = $LASTEXITCODE
Remove-Item $SqlFile -Force

if ($SqlExit -ne 0) {
    Write-Host "Echec sqlcmd (code $SqlExit)." -ForegroundColor Red
    exit $SqlExit
}

Write-Host "Compte admin cree !" -ForegroundColor Green
Write-Host "Email: $Email" -ForegroundColor Green
Write-Host "Roles: Administrateur + PharmacienTitulaire" -ForegroundColor Green
