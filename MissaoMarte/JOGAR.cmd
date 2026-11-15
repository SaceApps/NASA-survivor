@echo off
rem Sobe o Missao Marte a partir do codigo-fonte (precisa do .NET SDK 10) e abre o navegador quando estiver no ar.
rem Com "JOGAR.cmd --rede", tablets e celulares da mesma rede tambem entram.
rem Para quem nao tem o SDK: a versao compilada do zip de entrega (bash empacotar.sh) nao precisa instalar nada.
setlocal
title Missao Marte
set "DOTNET=dotnet"
rem o "dotnet" do PATH pode ser so o runtime, sem o SDK: nesse caso usa o SDK do perfil do usuario
dotnet --list-sdks 2>nul | findstr /r "[0-9]" >nul
if errorlevel 1 set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
if errorlevel 1 set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1
"%DOTNET%" run --project "%~dp0src\MissaoMarte.Web" -c Release -- --abrir %*
if errorlevel 1 pause
