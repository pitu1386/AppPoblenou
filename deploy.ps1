$ErrorActionPreference = "Stop"

# 1. Tailwind CSS (si hay Node instalado; si no, se usa el css/tailwind.css versionado)
if (Get-Command npm -ErrorAction SilentlyContinue) {
    Write-Host "Compilando Tailwind CSS..." -ForegroundColor Cyan
    Push-Location .\AtleticPoblenou
    if (-not (Test-Path .\node_modules\tailwindcss)) { npm install --no-audit --no-fund }
    npm run --silent build:css
    Pop-Location
    if ($LASTEXITCODE -ne 0) { Write-Error "Fallo la compilacion de Tailwind."; exit $LASTEXITCODE }
} else {
    Write-Host "npm no encontrado: se usa wwwroot/css/tailwind.css tal cual esta en el repositorio." -ForegroundColor Yellow
}

# 2. Publicacion .NET
Write-Host "Compilando version de produccion..." -ForegroundColor Cyan
dotnet publish .\AtleticPoblenou\AtleticPoblenou.csproj -c Release -o .\publish_output

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de produccion."
    exit $LASTEXITCODE
}

Write-Host "Configurando GitHub Pages (base href, .nojekyll, 404.html, blazor.webassembly.js)..." -ForegroundColor Cyan
New-Item -ItemType File -Force -Path ".\publish_output\wwwroot\.nojekyll" | Out-Null

# Copiar blazor.webassembly.js y dotnet.js sin huella hash para compatibilidad total
$wasmJs = Get-ChildItem -Path ".\publish_output\wwwroot\_framework\blazor.webassembly*.js" | Select-Object -First 1
if ($wasmJs -and ($wasmJs.Name -ne "blazor.webassembly.js")) {
    Copy-Item $wasmJs.FullName -Destination ".\publish_output\wwwroot\_framework\blazor.webassembly.js" -Force
}

$dotnetJs = Get-ChildItem -Path ".\publish_output\wwwroot\_framework\dotnet*.js" | Where-Object { $_.Name -notmatch "runtime|native" } | Select-Object -First 1
if ($dotnetJs) {
    Copy-Item $dotnetJs.FullName -Destination ".\publish_output\wwwroot\_framework\dotnet.js" -Force
}

# Ajustar base href y scripts en index.html
$indexPath = ".\publish_output\wwwroot\index.html"
$content = [System.IO.File]::ReadAllText($indexPath, [System.Text.Encoding]::UTF8)
$content = $content.Replace('<base href="/" />', '<base href="/AppPoblenou/" />')
if ($wasmJs) {
    $content = $content.Replace('_framework/blazor.webassembly.js', "_framework/$($wasmJs.Name)")
}
[System.IO.File]::WriteAllText($indexPath, $content, [System.Text.Encoding]::UTF8)
Copy-Item -Path $indexPath -Destination ".\publish_output\wwwroot\404.html" -Force

# 3. Push a gh-pages desde un directorio temporal aislado
Write-Host "Desplegando en la rama gh-pages de GitHub..." -ForegroundColor Cyan
$tempDeploy = Join-Path $env:TEMP "apn_ghpages_deploy"
if (Test-Path $tempDeploy) { Remove-Item $tempDeploy -Recurse -Force }
Copy-Item ".\publish_output\wwwroot" -Destination $tempDeploy -Recurse -Force

Push-Location $tempDeploy
git init -b gh-pages
git add -A
git commit -m "Deploy produccion"
git remote add origin https://github.com/pitu1386/AppPoblenou.git
git push -f origin gh-pages
Pop-Location

Remove-Item $tempDeploy -Recurse -Force

Write-Host "Despliegue completado con exito en gh-pages!" -ForegroundColor Green
Write-Host "URL: https://pitu1386.github.io/AppPoblenou/" -ForegroundColor Yellow
