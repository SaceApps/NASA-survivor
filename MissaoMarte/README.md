# Missão Marte

Jogo educativo cooperativo para crianças de 7 a 12 anos. Um tabuleiro hexagonal na mesa e um software que faz as
contas: a tripulação cuida do **ar**, da **água** e da **energia** de uma base na cratera Jezero durante 8 sóis,
explora o terreno e faz ciência. Quem cuida da base vence; quem não cuida, vê no relatório o que deu errado e por quê.

MVP feito num hackathon, em C# (.NET 10). Estado em 14/11/2026: jogável do começo ao fim, com 36 testes passando.

## Como rodar

Precisa do SDK do .NET 10.

| Para | Comando |
|---|---|
| Jogar neste computador | `dotnet run --project src/MissaoMarte.Web` e abrir <http://localhost:5080> |
| Jogar com tablets e celulares da mesma rede | `dotnet run --project src/MissaoMarte.Web -- --rede` e abrir `http://<IP do computador>:5080` nos aparelhos |
| Rodar os testes | `dotnet test` |
| Simular missões inteiras sem tela | `dotnet run --project src/MissaoMarte.Simulador` (com `-- detalhe` mostra cada jogada) |
| Gerar a missão de demonstração | `dotnet run --project src/MissaoMarte.Simulador -- demo src/MissaoMarte.Web/partidas` |
| Gerar os zips de entrega | `bash empacotar.sh` (Git Bash). Saem em `entrega/`: o zip completo (o jogo compilado, que abre por duplo clique sem instalar nada, e todos os arquivos do projeto), o mesmo conteúdo em duas partes de menos de 30 MB e um zip só com o código |

No Windows, `JOGAR.cmd` (duplo clique) sobe o jogo e abre o navegador. Com `--rede`, o Windows pode pedir para
liberar a porta 5080 no firewall. Sem `--rede`, só o próprio computador entra.

As partidas ficam em `src/MissaoMarte.Web/partidas`, um arquivo por missão, gravado a cada jogada aceita.

## Como se joga

1. **Manhã (8 h).** A tela Comando mostra o céu de Marte, os três sistemas e os objetivos. O que aconteceu de noite
   aparece aqui.
2. **Plano.** Cada pessoa tem 8 horas. Faz a rotina da profissão e escolhe tarefas: consertar, limpar os painéis,
   analisar amostra, cuidar de alguém, sair para explorar. A equipe decide o que fica ligado e com que prioridade.
3. **"E se a gente fizer assim?"** O botão de previsão roda o sol numa cópia e mostra como ele termina.
4. **Encerrar o sol.** O computador roda as 24 horas da física com o que foi combinado.
5. **Relatório.** O que aconteceu, por que aconteceu e o que dá para fazer, com gráficos hora a hora e um fato real
   da NASA.

O seletor "Texto para 7 a 8 / 9 a 10 / 11 a 12 anos" muda a profundidade: frase curta, causa e consequência, ou
causa, consequência e números. A idade escolhida ao criar a missão também muda os imprevistos e a força da tempestade.

### As profissões

| Profissão | Rotina (1 h) | Faz melhor |
|---|---|---|
| Comandante | Reunião da equipe: sobe o ânimo | Mantém a equipe trabalhando 8 ou 9 horas |
| Engenheiro(a) | Inspeção: as máquinas duram mais | Conserta em 3 h (os outros levam 6) e limpa os painéis em 2 h |
| Cientista | Traz a previsão do tempo de Marte | Analisa amostra em 2 h (os outros levam 4) |
| Geólogo(a) | Estuda imagens de órbita: revela 3 hexágonos | Coleta em 1 h e enxerga os hexágonos vizinhos |
| Médico(a) | Check-up: todos na base melhoram | Trata em 1 h e devolve mais saúde |
| Piloto | Revisão: o veículo gasta 15% menos | Sem piloto, o veículo gasta 50% mais energia e o dobro do tempo |

Cada profissão tem uma missão individual. A missão da equipe: chegar ao fim do sol 8 com todos bem, analisar
3 amostras e tirar 5 kg de água do solo de Marte. Explorar mais da metade do mapa vale bônus. São até 5 estrelas.

### Roteiro de demonstração (5 minutos)

1. Tela inicial: escolher "9 a 10 anos" e começar. Ou abrir "Demonstração: a tempestade", que já começa no sol 5.
2. Comando: mostrar o céu, os três sistemas e os objetivos. Tocar em "Prever o sol".
3. Tripulação: fazer a rotina de quem é cientista (traz a previsão) e de quem é engenheiro.
4. Mapa: escolher quem é geólogo, tocar no hexágono dois passos a leste da base e ir a pé. Aparece o cartão de
   ciência. Coletar a amostra e voltar.
5. Encerrar o sol. No relatório: o vazamento da vedação, os gráficos (passar o ponteiro mostra a régua) e o fato real.
6. Sistemas: mandar consertar a vedação. Desligar o gerador de oxigênio e prever de novo: a energia sobra, o oxigênio
   cai devagar.
7. Ciência: "Os números do jogo" (cada valor com a fonte) e "As contas fecham?" (massa e energia com diferença zero).
8. Trocar o texto para "7 a 8" e para "11 a 12" e mostrar o mesmo relatório nas três profundidades.

## Como está organizado

```
src/MissaoMarte.Nucleo      biblioteca sem tela: tudo o que é jogo mora aqui
  Simulacao/                o motor físico (ar, água, energia): Motor, Estado, Fisica, Parametros
  Jogo/                     as regras: Partida (jogadas), Partida.Sol (ciclo do sol e relatório), Mapa, Modelos
  Dados/                    parametros.json (150 valores com fonte) e nasa.json (fatos e imagens, offline)
src/MissaoMarte.Web         a tela (Blazor Server): só mostra o estado e envia as jogadas
src/MissaoMarte.Simulador   console que joga missões inteiras pelo núcleo
tests/MissaoMarte.Testes    36 testes: 11 do motor, 17 das regras e 8 dos achados da revisão
docs/                       especificação científica e a pesquisa das fontes
```

O motor é uma função determinística: recebe o estado e o que cada um faz naquela hora, devolve o estado seguinte.
Não sorteia nada. As regras chamam o motor 24 vezes por sol. A tela não faz conta nenhuma.

Detalhes do modelo, equações, hipóteses e fontes: [docs/ESPECIFICACAO_CIENTIFICA.md](docs/ESPECIFICACAO_CIENTIFICA.md).

## Requisitos do playbook

| Requisito | Situação | Onde |
|---|---|---|
| RF01 Criar e configurar partidas | Feito | Tela inicial: nome, idade da turma, tripulação |
| RF02 Cadastrar e selecionar tripulantes | Feito | Nome de quem joga em cada profissão; de 2 a 6 |
| RF03 Profissões e responsabilidades | Feito | 6 profissões com papel, rotina e vantagem |
| RF04 Missões principais e individuais | Feito | 3 objetivos obrigatórios, 1 bônus, 6 missões individuais, estrelas |
| RF05 Os dois tabuleiros hexagonais | Parcial | Superfície: 61 hexágonos jogáveis. Base: 7 módulos com o estado de cada um, sem movimentação dentro dela |
| RF06 Movimentação e exploração | Feito | A pé e de veículo, caminho mais barato pelo terreno, regras de segurança, descoberta |
| RF07 Ar | Feito | O2, CO2, N2, vapor, pressão, vazamento, reserva |
| RF08 Água | Feito | Potável, usada, tratamento com atraso, salmoura, água do solo |
| RF09 Energia | Feito | Sol por hora, poeira, bateria, prioridade das cargas |
| RF10 Sistemas simplificados | Parcial | Saúde, ânimo, desgaste e manutenção, radiação, temperatura, comunicação. Sono e alimentação entram só como consumo |
| RF11 Equipamentos e instalações | Feito | 11 equipamentos com chave, prioridade, condição e conserto |
| RF12 Eventos e ocorrências | Feito | Roteiro fixo por idade: vazamento, filtro, tempestade, removedor de CO2, gerador de O2 |
| RF13 Fontes de dados da NASA | Feito | Pacote offline com fonte, data e endereço; busca de imagens ao vivo opcional |
| RF14 Atualizar o estado da missão | Feito | Ciclo do sol, das 8 h às 8 h |
| RF15 Vitória e derrota | Feito | Vitória, missão incompleta e derrota, conferidas pelo software |
| RF16 Histórico das partidas | Feito | Diário, relatório de cada sol com as 24 horas, partidas salvas |
| RF17 Informações educativas | Feito | Relatório em três profundidades, cartões de ciência, central científica |
| RF18 Partidas cooperativas | Feito | Uma mesa compartilhada: todos os aparelhos veem e jogam a mesma missão |
| RNF01 Interface para crianças | Feito | Letra grande, botões largos, estado sempre com ícone, palavra e cor |
| RNF02 Navegador, celular e desktop | Feito | Conferido por foto em 1280 px e em 390 px |
| RNF03 Persistência | Feito | Um arquivo JSON por missão, gravado a cada jogada |
| RNF04 Integridade dos cálculos | Feito | Massa e energia conferidas a cada hora nos testes e na tela "As contas fecham?" |
| RNF05 Separar regras, interface e simulação | Feito | Três camadas; o simulador de console roda sem a tela |
| RNF06 Funcionar sem a NASA | Feito | Tudo o que o jogo precisa está no pacote offline |
| RNF07 Proteção dos dados | Feito | Só o nome digitado, guardado no próprio computador; sem conta; a busca ao vivo envia só o termo |
| RNF08 Atualizar cenários e missões | Parcial | Parâmetros e fatos em JSON; o mapa e o roteiro ainda estão em código (um arquivo cada) |
| RNF09 Versão dos modelos científicos | Feito | `versao_modelo` no catálogo (0.2.0) e a pesquisa datada em `docs/pesquisa` |
| RNF10 Acessibilidade e legibilidade | Parcial | Contraste, teclado, rótulos e cores validadas para daltonismo; não testado com leitor de tela |

## O que ficou de fora

- Movimentação dentro do tabuleiro da base e mais de uma missão ou cenário.
- Sono e alimentação como sistemas com decisões próprias.
- Várias mesas ao mesmo tempo no mesmo servidor (hoje é uma missão por vez).
- Clima real por data (o serviço da InSight está congelado em 2020; ficou registrado no pacote como referência).
- Teste com crianças. O equilíbrio foi conferido por simulação, não por jogo real.

## Limites conhecidos

- **Carregador do veículo.** A viagem desconta a bateria do veículo na hora da jogada, mas o motor só sabe onde o
  veículo terminou o sol. Um veículo que viajou à tarde e voltou começa a recarregar às 8 h; um que saiu à tarde e
  ficou fora não carrega nem nas horas em que esteve parado na base. O conserto é dar ao motor a posição do veículo
  hora a hora, como já é feito com as pessoas de traje.
- **Uma missão por vez no servidor.** "Sair para a tela inicial" tira todos os aparelhos da missão (ela fica salva).
- O código passou por uma revisão com três leituras independentes em 14/11/2026: 13 defeitos confirmados, 12
  consertados com teste e 1 registrado aqui.

## Créditos dos dados

Fatos, medidas e imagens: NASA, JPL-Caltech e parceiros, com a fonte e o crédito de cada item na tela Ciência.
As 10 imagens em `src/MissaoMarte.Web/wwwroot/nasa` são miniaturas da NASA Image and Video Library; antes de publicar
o jogo, conferir as diretrizes de uso de mídia da NASA.
