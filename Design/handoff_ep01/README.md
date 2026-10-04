# Handoff: EP01 "A Última Aula" → Unity

## Visão geral
Jogo de aventura point-and-click infantil (PC e mobile), inspirado em *Lost in Play*. Antônio, 8 anos, é abduzido pelo alien Amber durante a aula de educação física. O EP01 é o episódio de tutorial: 3 cenas na escola (sala, portão, quadra) + cutscene final, 10–15 min. Ensina andar, pegar itens, usar item no cenário, conversar e pedir dica.

Este pacote leva o protótipo jogável validado no navegador para um projeto Unity. **Não há código Unity ainda** — a tarefa é criar o projeto do zero.

## Sobre os arquivos de design
Os arquivos em `reference/` são **referências de design feitas em HTML**: protótipos que mostram aparência e comportamento, não código para copiar. Recriar em Unity (C#) com os padrões do Unity. Todo o conteúdo do episódio (cenas, hotspots, falas, puzzles, dicas) já foi extraído para `data/ep01.json` — essa é a fonte de verdade; o HTML serve para tirar dúvidas de comportamento.

Para jogar a referência: abrir `EP01 - Prototipo Jogavel.dc.html` no projeto original de design (ele depende de arquivos de runtime que não estão neste pacote).

## Fidelidade
- **Lógica, conteúdo e fluxo: alta fidelidade.** Falas, ordem dos puzzles, flags e dicas são finais para validação.
- **Arte: alta fidelidade** para cenários, Antônio, NPCs e itens.
- **UI (balão, inventário, dicas, puzzle): média fidelidade.** Seguir a estrutura e o comportamento; o visual pode ser refinado. Direção visual: plano, sem cantos arredondados, contornos de 2px em tinta escura, um único vermelho de destaque (tokens abaixo).

## Stack recomendada
- Unity 6 (ou 2022.3 LTS), template **2D (URP)**.
- Pacotes: Input System, 2D Sprite, TextMeshPro. Mais tarde: 2D Animation (rig do Antônio).
- Resolução de referência 1920×1080, câmera ortográfica, PPU 100 → palco de 19,2 × 10,8 unidades. Mobile: mesma proporção com letterbox.
- Dados: carregar `ep01.json` (Newtonsoft JSON via `com.unity.nuget.newtonsoft-json`) ou converter para ScriptableObjects por um importador de editor. Recomendado: importador que gera ScriptableObjects, para editar hotspots visualmente na cena.

## Sistema de coordenadas (importante)
Tudo no JSON está em **% do palco 16:9**.
- Hotspot `rect: [x, y, w, h]`: canto superior esquerdo, `y` medido **do topo**.
- Chão, personagens, `walk`, `walkTo`, `start`: `x` = centro horizontal em %, `y` = distância **da base** em %.
- Conversão para unidades Unity (palco centrado na origem, 19,2 × 10,8):
  `ux = (x/100 − 0.5) × 19.2`, `uyBase = (y/100 − 0.5) × 10.8`; para rects: `uyTop = (0.5 − y/100) × 10.8`.
- Cenários têm 2672×1456 (um pouco mais largos que 16:9). Exibir com **cover**: altura cheia, centralizado, corta ~42 px de cada lado.

## Cenas
Cada cena: fundo, área de chão, hotspots, falas de entrada (`intro`) e lista de objetivos (para as dicas). Detalhes completos em `data/ep01.json`.

1. **Sala de aula** (`sala`). Lápis quebra no caderno → Caio joga o apontador, que rola para baixo da carteira → pegar o braço mecânico na mochila → usar o braço no apontador → resolver 3 contas de multiplicação (pistas na janela, nos desenhos e no estojo) → mostrar à Prof. Lúcia → porta.
2. **Portão** (`portao`). Frisbee da Bia preso no alto do portão. Braço sozinho não alcança (`tried`) → falar com o Caio, que oferece as costas (`shoulders`, Antônio sobe 22%) → usar braço no frisbee → ganha o frisbee → caminho para a quadra.
3. **Quadra** (`quadra`). Corda enroscada no aro: braço não alcança, jogar o frisbee nela. Apito atrás da grade: pegar com o braço. Guardar tudo no saco de rede → turma sai para beber água → cutscene.
4. **Final** (`final`). 4 painéis verticais (640×1444) lado a lado, revelados um a um com fade de 1,2 s, 3,2 s cada, com legenda; depois tela de fim com tempo de jogo e "Jogar de novo".

Tela de título: botão "Começar" → `sala`.

## Mecânicas e comportamento

### Interação (point-and-click)
- Clique/toque no chão: Antônio anda até o ponto (limitado à área de chão).
- Clique num hotspot: Antônio anda até `walk` (só x; `null` = não anda), vira para o objeto e executa `tap`.
- Inventário: tocar num item seleciona (destaque); tocar de novo desseleciona. Com item selecionado, tocar num hotspot executa `use.<item>`; se não existir, fala uma das `failLines` aleatória.
- Durante uma sequência (`busy`) a entrada é ignorada, exceto clique para pular o balão atual.
- Passar o mouse sobre hotspot mostra o `label` (não mostrar quando há balão).
- Itens retirados do cenário: esconder o hotspot (`show`) e desenhar o `patchWhenTaken` por cima do fundo para "apagar" o objeto pintado.

### Sistema de regras
`tap` e `use.<item>` são listas de regras; executa a **primeira** cujo `if` é verdadeiro. Condições: `flags`, `notFlags`, `has`, `notHas`. Ações em `actionsDoc` no JSON. Implementar como um interpretador simples de ações (`async`/coroutines), executadas em sequência.

### Balões de fala
- Duração: `max(1,6 s, 60 ms × caracteres)`; clique pula.
- Posição: acima da cabeça de quem fala (Antônio: posição + altura escalada + lift; NPC: topo do rect). Limitar x a 17–83% e base a no máximo 76%.
- Mostra o nome do personagem em cima do texto.

### Dicas (3 níveis)
- Botão "Dica" sempre visível. Objetivo atual = primeiro item de `objectives` da cena cujo `if` é verdadeiro.
- Nível 1: alvo pulsa (contorno vermelho) e Antônio diz "Hm?". Nível 2: banner com a primeira frase do texto. Nível 3: banner com o texto completo + item sugerido pulsa no inventário.
- Dica automática nível 1 após 30 s sem input (configurável; 0 desliga). Qualquer ação zera o contador; concluir uma interação volta a dica para 0.

### Puzzle das contas
Painel tipo caderno com 3 linhas (`q = ?`) e 3 opções cada. Certo: preenche e mostra "Isso!"; errado: "Hm... não. Apaga e tenta de novo.". Botão de dica mostra a dica da primeira conta em aberto. Com as 3 certas aparece "Mostrar para a professora" → `onShow`. Fechar mantém as respostas.

### Movimento do Antônio (já validado no protótipo)
- **Área de chão em perspectiva** por cena: `nearY`/`farY` (bottom %), largura permitida interpolada de `nearX` para `farX`, escala interpolada de 1 (perto) para `farScale` (longe).
- Altura do Antônio = `antonioHeight` % × escala de profundidade.
- Movimento com aceleração: acel. = 140 %/s², velocidade máx. = 30 %/s × escala; desacelera para parar exatamente no alvo (`v² / 2a`). Distância vertical vale ×0,5625 (proporção 9/16).
- Sprite: `side` quando anda mais na horizontal (|dx| > |dy| × 0,6), espelhado conforme a direção; `front` quando parado.
- Animação procedural (até chegar o rig): fase do passo avança `distância / (4,2 × escala) × π`; balanço vertical `|sin(fase)| × 2,4%` da altura; comprime 5% no contato com o chão; inclina 5° na direção do movimento; respiração parada ±1,1%; sombra elíptica no pé que encolhe no pulo e some quando `lift > 3`.
- Ordenação: quem está mais perto da base desenha por cima (`sortingOrder` por y).

### NPCs
- Respiração contínua (escala ~1,6% vertical, ciclo 3,4 s, fase diferente por NPC).
- Falando: pulinho rápido (0,5 s).
- Inclina levemente na direção do Antônio quando ele está perto (< 28%); o Beto vira para ele.
- `npcMove`: anda até a nova posição em 1,1 s com passinhos (0,32 s).

## Novo: controle pelo teclado (PC)
Pedido do usuário para a versão Unity, além do clique:
- Setas / WASD movem o Antônio diretamente dentro da área de chão, com a mesma aceleração e frenagem.
- E / Espaço: interage com o hotspot mais próximo dentro do alcance (destacar o hotspot alcançável).
- 1–4: seleciona item do inventário; Esc: desseleciona / fecha o puzzle. H: dica.
- Mouse/toque continuam funcionando; mobile usa só toque.

## Estado
- `scene`, `flags` (dicionário string→valor), `inventory` (lista ordenada), `selectedItem`, `busy`, `hintLevel`, `idleSeconds`, `antonio { x, y, lift, dir }`, `puzzleAnswers`.
- Flags do EP01: `pulse`, `broken`, `thrown`, `sharp`, `mathDone`, `tried`, `shoulders`, `hasFrisbee`, `corda`, `apito`, `agua`.
- `debugStartStates` no JSON: estados para começar direto em uma cena durante o teste.
- Planejar salvamento (cena + flags + inventário) para a tela "Continuar".

## Tokens de design (UI)
- Fundo `#f3f2f2`, tinta `#201e1d`, destaque `#ec3013`; destaque claro (seleção) `oklch` ~ `#fde4dd`; texto de destaque em corpo pequeno usar um vermelho mais escuro (~`#a8200b`).
- Fonte: Archivo (Google Fonts) para tudo; títulos peso 800, corpo 400–600.
- Raio 0 em tudo; bordas de 2px na cor da tinta; sombras chapadas, sem desfoque.
- Pulso de dica: contorno vermelho 0,25% da largura do palco, escala 1→1,04, 1,2 s.

## Assets
- `assets/scenes/`: fundos 2672×1456 (sala, portão, quadra) e os 4 painéis do final (640×1444).
- `assets/characters/`: Antônio de frente (275×516) e de perfil (205×513); NPCs Caio, Bia, Lúcia, Beto (PNG com transparência, recortados).
- `assets/items/`: ícones do inventário.
- `assets/patches/`: recortes do fundo sem o objeto, para cobrir o item depois de pego.
- Pendentes / futuros: versões dos cenários sem objetos (os patches resolvem por ora), walk cycle ou rig do Antônio em partes, sons (no protótipo seriam sintetizados provisórios).

## Arquivos de referência (`reference/`)
- `EP01 - Prototipo Jogavel.dc.html`: protótipo jogável completo (lógica no `class Component`).
- `EP01 - A Ultima Aula.dc.html`: documento do episódio (roteiro, puzzles, briefings de arte).
- Fichas: Antônio, Amber, Ajudantes, Nave (episódios futuros).

## Primeiro pedido sugerido para o Claude Code
> Leia `README.md` e `data/ep01.json`. Crie um projeto Unity 2D URP com: carregador do JSON, palco 16:9, cena com fundo + hotspots, interpretador de regras/ações, Antônio com movimento em área de chão com perspectiva (clique e teclado), inventário, balões, dicas de 3 níveis, puzzle das contas, troca de cena com fade e cutscene final. Comece pela cena `sala` jogável de ponta a ponta, depois `portao` e `quadra`.
