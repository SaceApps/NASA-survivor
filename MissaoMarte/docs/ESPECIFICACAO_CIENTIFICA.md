# Especificação científica do motor

Versão do modelo: 0.2.0 (14/11/2026). Código: `src/MissaoMarte.Nucleo/Simulacao`. Valores: `Dados/parametros.json`.

## Princípios

1. **Nada aparece e nada some.** Toda massa que muda de lugar sai de um reservatório e entra em outro. O que atravessa
   a fronteira da base fica anotado no livro de fronteira, com o motivo. A energia gerada é igual à usada, mais a
   perdida, mais a desperdiçada, mais a variação da bateria.
2. **Sem sorteio.** O passo do motor é uma função do estado e da entrada. Os imprevistos são um roteiro fixo que muda
   a condição de um equipamento ou a poeira do céu; a consequência sai da conta.
3. **Nenhum número de física no código.** Os 88 valores que o motor e as regras usam vêm do catálogo, cada um com unidade, fonte,
   endereço e confiança. Faltou um, ou sobrou um desconhecido, o jogo não inicia.
4. **Passo de 1 hora marciana**: 1/24 de sol = 3698,97 s. Um sol tem 88.775,244 s.

Os testes conferem a conservação a cada hora, em missões inteiras com falhas e exploração. Diferença de massa
observada ao fim de 8 sóis: da ordem de 1e-12 kg (arredondamento de ponto flutuante).

## Tipos de valor

| Tipo | Quantos | O que é |
|---|---|---|
| `ciencia` | 40 | Constante ou medida publicada, lida na fonte em 14/11/2026 |
| `cenario` | 48 | Escolha de projeto desta base, com a referência que a sustenta |
| `referencia` | 62 | Informativo: aparece na tela, o motor não usa |

A pesquisa completa, com as notas de cada valor e as divergências entre fontes, está em
`docs/pesquisa/fontes_nasa_20261114.json`.

## Estado

Ar da cabine (kg de O2, CO2, N2 e vapor; temperatura), tanques de reserva de O2 e N2, água potável, água usada,
lotes em tratamento, salmoura, alimento seco e sua umidade, resíduos sólidos, solo esperando o forno, bateria,
sujeira dos painéis, bateria do veículo e 11 equipamentos (ligado, prioridade, condição).

## Ordem do passo

Geração solar → pedidos de potência → despacho por prioridade e bateria → tripulação → vazamento → remoção de CO2 →
geração de O2 → válvulas dos tanques → umidade → temperatura → água → forno → carregador do veículo.

## Equações

### Ar

- **Pressão parcial** de cada gás pela lei dos gases ideais: `p = (m / M) · R · T / V`. Volume da cabine: 300 m³.
- **Respiração**, por tripulante por dia terrestre (BVAD Rev 2, Tabela 3-31): consome 0,895 kg de O2 e produz
  1,085 kg de CO2. Um fator de atividade multiplica os dois: 0,75 dormindo, 1,125 acordado, 2,3 de traje.
  Os dois primeiros fecham em 1,0 na média do dia; o de traje vem do consumo de 0,086 kg/h de O2 em atividade externa.
- **De traje**, o oxigênio sai do tanque de reserva, e o CO2 e o vapor vão para fora (anotados no livro). O traje
  perde 0,006 kg/h.
- **Vazamento**: fração do ar por dia, igual para todos os gases. `taxa = nominal + (1 − condição da vedação) × máximo`.
- **Remoção de CO2**: proporcional à pressão parcial, até o dobro da nominal.
  `taxa = capacidade × condição × min(pCO2 / p_ref, 2)`, com 8,32 kg/dia a 0,40 kPa (dois leitos da classe do CDRA).
- **Geração de O2** por eletrólise: `2 H2O → 2 H2 + O2`, 1,126 kg de água por kg de O2. O hidrogênio é solto. Gasta
  9,3 kWh por kg de O2 (OGA da ISS, conjunto inteiro; o mínimo teórico é 4,12). Produz o que falta para 21,3 kPa,
  até 9,2 kg/dia, e nunca tira água abaixo da reserva de beber (20 kg), nem depois de a tripulação usar água na mesma hora.
- **Válvulas**: abaixo de 19,5 kPa de O2 a reserva repõe; o nitrogênio repõe a pressão total.
- **Umidade**: o desumidificador recolhe o vapor acima do alvo (50%), até 1,2 kg/h. Acima da saturação (fórmula de
  Magnus), o excesso condensa; a conta é refeita depois que a temperatura da hora muda, para a umidade nunca passar
  de 100%. Nos dois casos a água vai para o tanque de água usada.

### Água

- **O corpo é passagem.** Entram 2,717 kg de bebida, 0,5 de preparo do alimento, 0,76 de água no alimento e 0,49 de
  água metabólica: 4,467 kg. Saem 1,42 na urina, 2,946 como vapor e suor e 0,101 nas fezes: 4,467 kg.
- **Metabolismo fecha em massa**: `comida seca + O2 = CO2 + água metabólica + resíduo seco`
  (0,800 + 0,895 = 1,085 + 0,490 + 0,120).
- **Higiene e descarga**: 2,12 + 0,5 kg por pessoa por dia, de potável para usada. Com economia, 40% da higiene.
- **Tratamento**: até 5,9 kg/h entram no processador; cada lote leva 3 horas; 98% volta como potável e 2% vira
  salmoura. Com o filtro saturado, a capacidade cai com a condição e a recuperação vale `0,98 × (0,6 + 0,4 × condição)`.
- **Forno**: o solo argiloso entra pela fronteira; 4% da massa sai como água (vai para o tratamento) e o solo seco
  volta para fora. Gasta 4 kWh por kg de água.

### Energia

- **Sol no topo da atmosfera**: `S = 1361 / r²`, com `r = a(1 − e²) / (1 + e·cos(Ls − Ls_periélio))` em UA.
- **Altura do Sol**: `cos z = sen(lat)·sen(decl) + cos(lat)·cos(decl)·cos(h)`, com `decl = asen(sen(obliquidade)·sen(Ls))`,
  latitude 18,44° N (pouso do Perseverance) e Ls inicial 5,6°.
- **Poeira**: `superfície = topo × cos z × exp(−k·tau)`, com k = 0,33. O k foi calibrado com dois pontos públicos do
  rover Opportunity em 2018 (645 Wh com tau 0,6 e 22 Wh com tau 10,8) e conferido contra 2007, com erro de ±20%.
  Vale para a energia do dia inteiro em painel horizontal. Céu limpo em Jezero: tau 0,45.
- **Painéis**: 240 m², 23% de eficiência (27% da célula × 0,85 de painel), menos a sujeira. A poeira assenta 0,28%
  por sol (Pathfinder), multiplicada por `tau / tau_limpo` quando o céu está carregado.
- **Despacho**: a energia disponível na hora é a solar mais o que a bateria entrega, menos 3% de distribuição. As
  cargas são atendidas por prioridade. As fixas são tudo ou nada; as variáveis (gerador de O2, aquecimento, forno,
  carregador) aceitam parte.
- **Bateria**: 160 kWh, 95% na carga e 95% na descarga, até 15 kW.

### Temperatura

Modelo de um nó: `C · dT/dt = aquecimento + calor interno − UA · (T − T_fora)`, com UA = 45 W/K e C = 2,5e7 J/K.
O calor interno é o calor sensível das pessoas (73 W cada) mais 80% da potência das máquinas internas. Fora, a
temperatura segue uma senoide entre −83 °C e −22 °C (medidas do Perseverance), com pico às 14 h.

## Limites usados pelo jogo

| Grandeza | Limite | Origem |
|---|---|---|
| O2 no ar | alerta 19,5 kPa; perigo 17,2; crítico 15,2 | Projeto da ISS; BVAD Rev 2, Tabela 4-1 |
| CO2 no ar | normal até 0,40 kPa; agir em 1,01; emergência em 2,0 | NASA-STD-3001; regras de voo da ISS |
| Temperatura | confortável de 18 a 27 °C | NASA-STD-3001 |
| Umidade | confortável de 25 a 75% | BVAD Rev 2, Tabela 4-1 |

## Sistemas simplificados (regras de jogo, não física)

Saúde (cai por hora quando um limite é passado, sobe dormindo), ânimo, desgaste das máquinas (cresce 12% por sol sem
inspeção), dose de radiação (0,67 mSv/dia na superfície, metade dentro da base) e os tempos das tarefas. Esses
números ficam no código das regras e são escolhas de jogo.

## Hipóteses e limites conhecidos

- Ar perfeitamente misturado; um único volume; sem gases-traço.
- Passo de 1 hora: quem fica 20 minutos de traje conta como a hora cheia do lado de fora.
- A estação (Ls) avança de modo uniforme; em 8 sóis a diferença é desprezível.
- O `exp(−k·tau)` é uma aproximação da energia diária aplicada hora a hora.
- A fração de água do solo (4%) e a energia do forno (4 kWh/kg) são hipóteses sem fonte conferida nesta versão.
  Jezero fica fora da faixa com gelo raso confirmado (acima de 35° N), por isso a água local do jogo vem de
  minerais hidratados.
- A potência do processador de água (700 W) é metade do teto de requisito da ISS (1355 W); não há média medida.
- A área dos painéis e a bateria foram dimensionadas para o jogo: folga de cerca de 25% em céu limpo e bateria
  insuficiente para atravessar a tempestade sem desligar nada.

## Verificação

`dotnet test`: 36 testes. Os do motor conferem massa e energia a cada hora, o acúmulo de CO2 contra uma conta
independente, a eletrólise, a prioridade de energia, o atraso e a perda do tratamento de água, a atenuação pela
poeira, a geometria solar e o determinismo. Os do jogo conferem as regras, o ciclo do sol, salvar e carregar, a
previsão e missões inteiras nos três níveis. Outros 8 prendem os defeitos achados na revisão de 14/11/2026.
