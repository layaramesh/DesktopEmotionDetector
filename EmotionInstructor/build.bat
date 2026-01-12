@echo off
echo ====================================
echo  Emotion Instructor - Build Script
echo ====================================
echo.

echo [1/3] Cleaning previous build...
dotnet clean

echo.
echo [2/3] Building application...
dotnet build

echo.
echo [3/3] Copying ONNX models...
copy "..\Models\emotion_cnn.onnx" "bin\Debug\net8.0-windows\" >nul 2>&1
copy "..\Models\emotion-ferplus-8.onnx" "bin\Debug\net8.0-windows\" >nul 2>&1

echo.
echo ====================================
echo  Build Complete!
echo ====================================
echo.
echo To run the application:
echo   1. Use: dotnet run
echo   2. Or:  .\bin\Debug\net8.0-windows\EmotionInstructor.exe
echo.
pause
