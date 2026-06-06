@echo off
title BR Launcher - Iniciando...
echo =======================================
echo Iniciando o BrLauncher (Modo Desenvolvedor)
echo =======================================
echo.
echo Compilando as ultimas alteracoes (HTML/CSS/JS/C#)...
dotnet build
echo.
echo Iniciando o Launcher...
start "" "bin\Debug\net5.0-windows\BrLauncher.exe"
exit
