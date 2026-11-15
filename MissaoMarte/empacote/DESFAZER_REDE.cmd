@echo off
rem Remove a liberacao do firewall criada pelo LIBERAR_REDE.cmd. Precisa de permissao de administrador.
net session >nul 2>&1
if errorlevel 1 goto elevar
netsh advfirewall firewall delete rule name="Missao Marte (jogo)"
echo.
echo Pronto: a liberacao foi removida.
pause
goto fim
:elevar
echo Pedindo permissao de administrador...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
:fim
