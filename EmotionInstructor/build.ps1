# Emotion Instructor - Build and Run Script

Write-Host "====================================" -ForegroundColor Cyan
Write-Host " Emotion Instructor - Build Script" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/3] Cleaning previous build..." -ForegroundColor Yellow
dotnet clean | Out-Null

Write-Host ""
Write-Host "[2/3] Building application..." -ForegroundColor Yellow
dotnet build

Write-Host ""
Write-Host "[3/3] Copying ONNX models..." -ForegroundColor Yellow
Copy-Item "..\Models\emotion_cnn.onnx" "bin\Debug\net8.0-windows\win-x64\" -ErrorAction SilentlyContinue
Copy-Item "..\Models\emotion-ferplus-8.onnx" "bin\Debug\net8.0-windows\win-x64\" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "====================================" -ForegroundColor Green
Write-Host " Build Complete!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""
Write-Host "To run the application:" -ForegroundColor White
Write-Host "  1. Use: " -NoNewline -ForegroundColor White
Write-Host "dotnet run" -ForegroundColor Cyan
Write-Host "  2. Or:  " -NoNewline -ForegroundColor White
Write-Host ".\bin\Debug\net8.0-windows\win-x64\EmotionInstructor.exe" -ForegroundColor Cyan
Write-Host ""
