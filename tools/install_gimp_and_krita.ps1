# Resumable, UAC-free Windows installer for Krita (Portable) & GIMP (Current-User)
# Installs cleanly to D:\KenyaStudioTools\Krita and %LOCALAPPDATA%\Programs\GIMP 3
$ErrorActionPreference = 'Stop'
$dir = 'D:\KenyaStudioTools'
New-Item -ItemType Directory -Path $dir -Force | Out-Null

Write-Host "=== 1/2: Krita 5.2.9 Portable (Free Digital Painting & Livery Editor) ===" -ForegroundColor Cyan
$kritaZip = "$dir\krita-x64-5.2.9.zip"
$kritaDir = "$dir\Krita"
if (-not (Test-Path "$kritaDir\krita-x64-5.2.9\bin\krita.exe")) {
    curl.exe -C - -L --progress-bar -o $kritaZip "https://download.kde.org/stable/krita/5.2.9/krita-x64-5.2.9.zip"
    Expand-Archive -Path $kritaZip -DestinationPath $kritaDir -Force
    Remove-Item $kritaZip -Force
}
$kritaExe = (Get-ChildItem $kritaDir -Recurse -Filter "krita.exe" | Select-Object -First 1).FullName
if ($kritaExe) {
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Krita.lnk")
    $sc.TargetPath = $kritaExe
    $sc.Save()
    Write-Host "[READY] Krita installed at: $kritaExe" -ForegroundColor Green
}

Write-Host "`n=== 2/2: GIMP 3.2.8 (Free Texture, Signboard & PBR Editor) ===" -ForegroundColor Cyan
$gimpExe = (Get-ChildItem "$env:LOCALAPPDATA\Programs\GIMP*" -Recurse -Filter "gimp-*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
if (-not $gimpExe) {
    $gimpSetup = "$dir\gimp-3.2.8-setup.exe"
    curl.exe -C - -L --progress-bar -o $gimpSetup "https://download.gimp.org/gimp/v3.2/windows/gimp-3.2.8-setup.exe"
    Start-Process -FilePath $gimpSetup -ArgumentList "/CURRENTUSER /VERYSILENT /NORESTART" -Wait
    Remove-Item $gimpSetup -Force
    $gimpExe = (Get-ChildItem "$env:LOCALAPPDATA\Programs\GIMP*" -Recurse -Filter "gimp-*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}
if ($gimpExe) {
    Write-Host "[READY] GIMP installed at: $gimpExe" -ForegroundColor Green
}
