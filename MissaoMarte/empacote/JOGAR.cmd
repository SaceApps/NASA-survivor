@echo off
rem Abre o Missao Marte (versao compilada: nao precisa instalar nada). Extraia o zip inteiro antes.
rem Se o jogo veio em dois zips (parte1de2 e parte2de2), este arquivo junta as duas partes sozinho.
title Missao Marte
set "PASTA=%~dp0jogo"
set "JOGO=%PASTA%\MissaoMarte.Web.exe"
if not exist "%JOGO%" goto semjogo
if exist "%PASTA%\System.Private.CoreLib.dll" goto abrir
rem Falta a parte 2 (o resto do runtime). Se ela foi extraida numa pasta vizinha, junta aqui.
set "PARTE2="
for /d %%D in ("%~dp0..\..\*parte2*") do if exist "%%D\MissaoMarte\jogo\System.Private.CoreLib.dll" set "PARTE2=%%D\MissaoMarte\jogo"
if not defined PARTE2 goto semparte2
echo Juntando a parte 2 do jogo (so na primeira vez)...
xcopy "%PARTE2%\*" "%PASTA%\" /e /y /q >nul
if not exist "%PASTA%\System.Private.CoreLib.dll" goto semparte2
:abrir
"%JOGO%" --abrir %*
if errorlevel 1 pause
goto fim
:semparte2
echo.
echo Falta a PARTE 2 do jogo.
echo O jogo veio em dois zips: parte1de2 e parte2de2. Extraia os dois.
echo Se ja extraiu, copie a pasta "jogo" da parte 2 para dentro desta pasta
echo (onde esta este JOGAR.cmd) e deixe o Windows juntar as duas pastas "jogo".
echo.
pause
goto fim
:semjogo
echo Nao achei a pasta "jogo" ao lado deste arquivo.
echo Extraia o zip inteiro para uma pasta e abra o JOGAR.cmd de la.
pause
:fim
