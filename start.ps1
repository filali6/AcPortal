# 1. Docker (pas besoin de terminal, juste la commande)
Write-Host "Démarrage Docker..." -ForegroundColor Cyan
cd $PSScriptRoot\Backend
docker-compose up -d
Start-Sleep -Seconds 5

# 2. Dapr en arrière plan
Write-Host "Démarrage Dapr..." -ForegroundColor Cyan
$dapr = Start-Process -FilePath "daprd" -ArgumentList "--app-id acp-backend --app-port 5281 --dapr-http-port 3500 --dapr-grpc-port 50002 --resources-path $PSScriptRoot\Backend\dapr\components --app-health-probe-interval 30 --log-level info" -PassThru -WindowStyle Minimized
Start-Sleep -Seconds 5

# 3. Backend en arrière plan
Write-Host "Démarrage Backend..." -ForegroundColor Cyan
$backend = Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory "$PSScriptRoot\Backend" -PassThru -WindowStyle Minimized
Start-Sleep -Seconds 3

# 4. Frontend en arrière plan
Write-Host "Démarrage Frontend..." -ForegroundColor Cyan
$frontend = Start-Process -FilePath "ng" -ArgumentList "serve" -WorkingDirectory "$PSScriptRoot\Frontend" -PassThru -WindowStyle Minimized

Write-Host "Tout est démarré ! Dapr + Backend + Frontend tournent en arrière plan." -ForegroundColor Green
Write-Host "Pour tout arrêter ferme les fenêtres minimisées ou relance le terminal." -ForegroundColor Yellow