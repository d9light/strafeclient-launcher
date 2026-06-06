@echo off
title Strafe Client - Iniciando...
echo =======================================
echo Iniciando o Strafe Client (Modo Desenvolvedor)
echo =======================================
echo.
echo Compilando as ultimas alteracoes (HTML/CSS/JS/C#)...
dotnet build
echo.
echo Iniciando o Launcher...
start "" "bin\Debug\net5.0-windows\StrafeClient.exe"
exit
