# Northwind veritabanını sıfırdan kurar (test verilerini temizlemek için).
# Kullanım:  cd C:\Projeler\Northwind\docs\db ;  powershell -ExecutionPolicy Bypass -File .\reset_db.ps1
# Uyarı: northwind veritabanındaki TÜM değişiklikler silinir.

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c client_min_messages=warning"

$pgPass  = Read-Host "postgres kullanıcısının parolası" -AsSecureString
$appPass = Read-Host "nw_app parolası (appsettings.Development.json'daki ile aynı)" -AsSecureString
$roPass  = Read-Host "nw_readonly parolası (SQL Console bağlantısı için)" -AsSecureString
$env:PGPASSWORD = [System.Net.NetworkCredential]::new("", $pgPass).Password
$app = [System.Net.NetworkCredential]::new("", $appPass).Password
$ro  = [System.Net.NetworkCredential]::new("", $roPass).Password

function Run([string[]]$psqlArgs) {
    & psql -U postgres -v ON_ERROR_STOP=1 -q @psqlArgs
    if ($LASTEXITCODE -ne 0) { throw "psql hata verdi: $($psqlArgs -join ' ')" }
}

try {
    Write-Host "1/7 Eski veritabanı siliniyor (açık bağlantılar kapatılır)..."
    Run @("-c", "DROP DATABASE IF EXISTS northwind WITH (FORCE)")
    Run @("-c", "DROP ROLE IF EXISTS nw_app")
    Run @("-c", "DROP ROLE IF EXISTS nw_readonly")
    Write-Host "2/7 Veritabanı oluşturuluyor..."
    Run @("-c", "CREATE DATABASE northwind ENCODING 'UTF8' TEMPLATE template0")
    Write-Host "3/7 01_northwind.sql"
    Run @("-d", "northwind", "-f", "01_northwind.sql")
    Write-Host "4/7 02_app_extensions.sql"
    Run @("-d", "northwind", "-f", "02_app_extensions.sql")
    Write-Host "5/7 03_sample_data.sql"
    Run @("-d", "northwind", "-f", "03_sample_data.sql")
    Write-Host "6/7 05_app_role.sql"
    Run @("-d", "northwind", "-f", "05_app_role.sql")
    Run @("-c", "ALTER ROLE nw_app PASSWORD '$app'")
    Write-Host "     06_console_role.sql"
    Run @("-d", "northwind", "-f", "06_console_role.sql")
    Run @("-c", "ALTER ROLE nw_readonly PASSWORD '$ro'")
    Write-Host "7/7 Kontrol (beklenen: 1265793.04 | 830):"
    & psql -U postgres -d northwind -At -c "SELECT ROUND(SUM(net_amount),2) || ' | ' || (SELECT COUNT(*) FROM orders) FROM v_order_lines"
    Write-Host "Tamam."
}
finally {
    Remove-Item Env:PGPASSWORD, Env:PGOPTIONS -ErrorAction SilentlyContinue
}
