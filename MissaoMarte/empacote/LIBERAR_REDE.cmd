@echo off
rem Libera as portas do jogo (5080 a 5089) no firewall do Windows, para tablets e celulares da mesma rede.
rem Sem nada: vale para redes marcadas como Privada. Com "publica": vale para qualquer rede (use so no evento).
rem Precisa de permissao de administrador: o Windows pergunta.
net session >nul 2>&1
if errorlevel 1 goto elevar
set PERFIL=private,domain
if /i "%~1"=="publica" set PERFIL=any
netsh advfirewall firewall delete rule name="Missao Marte (jogo)" >nul 2>&1
netsh advfirewall firewall add rule name="Missao Marte (jogo)" dir=in action=allow protocol=TCP localport=5080-5089 profile=%PERFIL%
echo.
echo Pronto: portas 5080 a 5089 liberadas (perfil: %PERFIL%). Agora abra o JOGAR_NA_REDE.cmd.
echo Para desfazer: DESFAZER_REDE.cmd
pause
goto fim
:elevar
echo Pedindo permissao de administrador...
if "%~1"=="" goto elevarsimples
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%~1' -Verb RunAs"
goto fim
:elevarsimples
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
:fim
