@echo off
rem Abre o Missao Marte para tablets e celulares da mesma rede. O endereco para digitar nos aparelhos aparece na janela.
rem Antes, uma vez so: LIBERAR_REDE.cmd (firewall do Windows).
call "%~dp0JOGAR.cmd" --rede %*
