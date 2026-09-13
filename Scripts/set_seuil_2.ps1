$password = Read-Host "Mot de passe Azure SQL" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

$query = @"
-- Mettre AlertThreshold = 2 pour tous les produits qui ont encore 0
UPDATE Products
SET AlertThreshold = 2
WHERE AlertThreshold = 0
OR AlertThreshold IS NULL;

PRINT 'Produits mis a jour : ' + CAST(@@ROWCOUNT AS VARCHAR);

-- Verifier
SELECT
    AlertThreshold,
    COUNT(*) as NbProduits
FROM Products
GROUP BY AlertThreshold
ORDER BY AlertThreshold;
"@

sqlcmd -S "tcp:pharmacie-saintjeanpaul-srv.database.windows.net,1433" -d "pharmacie-db" -U "pharmacieadmin" -P $plain -Q $query -C -I

Write-Host "Termine." -ForegroundColor Green
