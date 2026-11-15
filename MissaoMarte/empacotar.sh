#!/usr/bin/env bash
# Gera os zips de entrega em entrega/, a partir do último commit (com mudança pendente, o script para):
#   MissaoMarte-completo-<versão>.zip   tudo num zip só: o jogo compilado (roda sem instalar nada em Windows de
#                                       64 bits) e todos os arquivos do projeto. Para pendrive ou rede local.
#   MissaoMarte-parte1de2-<versão>.zip  o mesmo conteúdo em duas partes de menos de 30 MB, para canais com limite
#   MissaoMarte-parte2de2-<versão>.zip  de tamanho. O JOGAR.cmd junta as duas sozinho.
#   MissaoMarte-codigo-<versão>.zip     só os arquivos do projeto (código, testes, documentação).
# uso: bash empacotar.sh     (na raiz do repositório; na primeira vez precisa de internet, para o pacote do runtime)
set -euo pipefail
cd "$(dirname "$0")"
if [ -n "$(git status --porcelain)" ]; then echo "Há mudanças sem commit. Faça o commit antes de empacotar."; exit 1; fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
DN=dotnet
if ! dotnet --list-sdks 2>/dev/null | grep -q '[0-9]'; then   # o dotnet do PATH pode ser só o runtime, sem o SDK
  export DOTNET_ROOT="$LOCALAPPDATA/Microsoft/dotnet"
  DN="$DOTNET_ROOT/dotnet.exe"
fi
TAR=/c/Windows/System32/tar.exe
LIMITE=$((29 * 1024 * 1024))                    # cada parte tem de ficar abaixo de 30 MB, com folga

VERSAO="$(git log -1 --format=%cd --date=format:%Y-%m-%d)-$(git rev-parse --short HEAD)"
SAIDA="entrega"
MONTAGEM="$SAIDA/montagem"
PASTA="$MONTAGEM/MissaoMarte"                   # o mesmo nome nas duas partes: extraídas no mesmo lugar, elas se juntam
rm -rf "$MONTAGEM" "$SAIDA"/MissaoMarte-*.zip
mkdir -p "$PASTA/jogo" "$PASTA/codigo-fonte"

echo "1/6 testes"
if ! saida="$("$DN" test -c Release -v q 2>&1)"; then    # nada vai para o zip com teste falhando
  echo "$saida" | tail -20
  echo "Teste falhando: nada foi empacotado."
  exit 1
fi
echo "$saida" | grep -E "Aprovado" | tail -1

echo "2/6 jogo compilado (com o runtime junto)"
if ! saida="$("$DN" publish src/MissaoMarte.Web -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o "$PASTA/jogo" -v q 2>&1)"; then
  echo "$saida" | tail -20
  echo "A compilação falhou: nada foi empacotado."
  exit 1
fi
test -f "$PASTA/jogo/MissaoMarte.Web.exe"
test -f "$PASTA/jogo/wwwroot/jogo.css"
test -f "$PASTA/jogo/System.Private.CoreLib.dll"     # é por este arquivo que o JOGAR.cmd sabe se a parte 2 chegou

echo "3/6 missão de demonstração"
mkdir -p "$PASTA/jogo/partidas"
"$DN" run -c Release --no-build --project src/MissaoMarte.Simulador -- demo "$PASTA/jogo/partidas" | tail -1

echo "4/6 todos os arquivos do projeto e os atalhos"
git archive HEAD | tar -x -C "$PASTA/codigo-fonte"
for arquivo in empacote/*; do                       # arquivos de abrir no Windows: fim de linha CRLF
  sed 's/\r$//; s/$/\r/' "$arquivo" > "$PASTA/$(basename "$arquivo")"
done
sed -i "s/@VERSAO@/$VERSAO/" "$PASTA/LEIA-ME.txt"
printf '\xEF\xBB\xBF' | cat - "$PASTA/LEIA-ME.txt" > "$PASTA/LEIA-ME.tmp"   # marca de UTF-8 para o Bloco de Notas
mv "$PASTA/LEIA-ME.tmp" "$PASTA/LEIA-ME.txt"

echo "5/6 zips"
( cd "$MONTAGEM" && "$TAR" -a -c -f "../MissaoMarte-completo-$VERSAO.zip" MissaoMarte )
# as duas partes: a parte 2 leva metade do runtime (os System.* de A a R); a parte 1 leva todo o resto
( cd "$MONTAGEM" && find MissaoMarte -type f | sort > lista_tudo.txt )
grep -E '^MissaoMarte/jogo/System\.[A-R]' "$MONTAGEM/lista_tudo.txt" > "$MONTAGEM/lista_parte2.txt"
grep -vE '^MissaoMarte/jogo/System\.[A-R]' "$MONTAGEM/lista_tudo.txt" > "$MONTAGEM/lista_parte1.txt"
( cd "$MONTAGEM" && "$TAR" -a -c -f "../MissaoMarte-parte1de2-$VERSAO.zip" -T lista_parte1.txt )
( cd "$MONTAGEM" && "$TAR" -a -c -f "../MissaoMarte-parte2de2-$VERSAO.zip" -T lista_parte2.txt )
git archive --format=zip --prefix=MissaoMarte-codigo/ -o "$SAIDA/MissaoMarte-codigo-$VERSAO.zip" HEAD

echo "6/6 conferência"
conta() { "$TAR" -tf "$1" | grep -vc '/$'; }
total="$(wc -l < "$MONTAGEM/lista_tudo.txt")"
p1="$(conta "$SAIDA/MissaoMarte-parte1de2-$VERSAO.zip")"
p2="$(conta "$SAIDA/MissaoMarte-parte2de2-$VERSAO.zip")"
inteiro="$(conta "$SAIDA/MissaoMarte-completo-$VERSAO.zip")"
echo "arquivos: completo $inteiro; parte 1 $p1 + parte 2 $p2 = $((p1 + p2)) (montados: $total)"
[ "$inteiro" -eq "$total" ] && [ "$((p1 + p2))" -eq "$total" ] || { echo "A soma das partes não bate com o todo."; exit 1; }
"$TAR" -tf "$SAIDA/MissaoMarte-parte2de2-$VERSAO.zip" > "$MONTAGEM/conteudo_parte2.txt"
grep -q 'jogo/System.Private.CoreLib.dll' "$MONTAGEM/conteudo_parte2.txt" || { echo "A parte 2 saiu sem o arquivo que a identifica."; exit 1; }
for parte in parte1de2 parte2de2 codigo; do
  tamanho="$(stat -c %s "$SAIDA/MissaoMarte-$parte-$VERSAO.zip")"
  [ "$tamanho" -lt "$LIMITE" ] || { echo "MissaoMarte-$parte passou do limite: $tamanho bytes."; exit 1; }
done
rm -rf "$MONTAGEM"
ls -la "$SAIDA" | grep zip
echo "pronto: $SAIDA/"
