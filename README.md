# Recreio Espacial — EP01 "A Última Aula" (Unity)

Aventura point-and-click infantil (PC e mobile) inspirada em *Lost in Play*. Antônio, 8 anos, é abduzido
pelo alien Amber durante a aula de educação física. O EP01 é o episódio-tutorial: sala de aula, portão,
quadra e a cutscene final.

Este projeto é a versão Unity do protótipo feito no Claude Design. O briefing original (o "handoff") está em
[`Design/handoff_ep01/README.md`](Design/handoff_ep01/README.md), junto com as fichas de personagem e o
protótipo em HTML.

---

## Como abrir

1. Instale o **Unity Hub** e o editor **Unity 6.6 (6000.6.4f1)**, a versão usada no projeto.
   O pacote Input System está na 1.20.0 (versões antigas, como a 1.11, não compilam no Unity 6.6).
2. No Hub: **Add → Add project from disk** e escolha a pasta deste repositório.
   - Se o Hub disser que a versão `6000.0.58f2` não está instalada, escolha a versão Unity 6 que você tem.
3. A primeira abertura demora (o Unity importa as imagens e cria a pasta `Library/`).
   - O projeto usa o Input System (Project Settings → Player → Active Input Handling).
     O código também funciona com o Input Manager antigo, se precisar.
4. Abra a cena `Assets/Scenes/Main.unity` e aperte **Play**.

> O jogo se monta sozinho por código ao apertar Play (câmera, cenário, personagens e interface).
> Na cena só existe o objeto **Recreio Espacial**, que guarda as opções de teste.

## Controles

| Ação | Mouse / toque | Teclado |
|---|---|---|
| Andar | tocar no chão | setas / WASD |
| Interagir | tocar no objeto ou pessoa | **E** ou **Espaço** (no objeto destacado) |
| Usar item | tocar no item e depois no objeto | **1–4** escolhem o item; depois E |
| Pular fala | tocar no balão / em qualquer lugar | E / Espaço |
| Dica | botão **Dica** | **H** |
| Cancelar | tocar no item de novo | **Esc** (também fecha o caderno) |

## Atalhos de teste (só no Editor)

- **F1** — mostra/esconde o contorno de todos os hotspots.
- **F5 / F6 / F7 / F8** — começa direto na sala / portão / quadra / final, com o estado de
  `debugStartStates` do JSON.
- No objeto **Recreio Espacial** (Inspector): `Start Scene` (title, sala, portao, quadra, final),
  `Show Hotspots` e `Auto Hint Seconds` (0 desliga a dica automática).
- Menu **Recreio Espacial → Apagar jogo salvo** e **Reimportar arte do EP01**.

## Onde está cada coisa

```
Assets/
  Resources/
    EP01/ep01.json        ← o episódio inteiro: cenas, hotspots, falas, regras, puzzles, dicas
    EP01/assets/...       ← cenários, personagens, itens e patches (mesmos caminhos do JSON)
    Fonts/                ← Archivo (licença OFL)
  Scenes/Main.unity       ← cena de entrada
  Scripts/
    Data/                 ← leitura do JSON (MiniJson) e modelo de dados (EpisodeData)
    Core/                 ← GameController (maestro), GameState (flags/inventário/save),
                            Stage (coordenadas % ↔ Unity), GameInput, GameAssets
    World/                ← AntonioController, NpcActor, SceneView, RectOutline
    UI/                   ← GameUI (toda a interface) e UiKit (estilo visual)
    Editor/               ← importação automática das imagens + menu
Design/handoff_ep01/      ← briefing, roteiro, fichas e protótipo HTML (referência)
```

## Como o jogo funciona

- **O JSON manda.** `ep01.json` é a fonte de verdade: para mudar uma fala, um hotspot, a ordem de um
  puzzle ou o texto de uma dica, edite o JSON e aperte Play de novo. Não precisa mexer em C#.
- **Coordenadas em % do palco 16:9.** Hotspots usam `[x, y, largura, altura]` com `y` medido do topo;
  chão e personagens usam `y` medido da base. `Stage.cs` converte para unidades do Unity
  (palco de 19,2 × 10,8 centrado na origem).
- **Regras e ações.** Cada `tap` ou `use.<item>` é uma lista de regras; vale a primeira cujo `if`
  é verdadeiro. As ações (`say`, `set`, `give`, `walkTo`, `goScene`, `npcMove`, `lift`, …) rodam em
  sequência como corrotinas no `GameController`. Enquanto uma sequência roda, o jogo fica "ocupado"
  e um clique só pula o balão.
- **Dicas em 3 níveis.** O objetivo atual é o primeiro de `objectives` cujo `if` é verdadeiro.
  Nível 1: o alvo pulsa em vermelho. Nível 2: faixa com a primeira frase. Nível 3: texto completo e
  o item sugerido pulsa no inventário. Depois de 30 s parado, a dica nível 1 aparece sozinha.
- **Salvamento.** Ao entrar em cada cena o jogo salva (cena, flags, inventário). "Continuar" na tela
  de título recomeça a última cena com o progresso das anteriores.

### Acréscimos ao JSON do handoff

Para tirar regras que estavam escritas só em texto e deixá-las como dados:

- `items.*.article` — artigo do item ("a corda", "o apito") usado pela ação `sayMissing`.
- `scenes.portao.restrictWhenFlag` — a regra "nas costas do Caio": só frisbee e Caio respondem, os
  outros objetos dizem "Daqui de cima só alcanço o frisbee." e o Antônio não anda.

### Diferenças em relação ao protótipo HTML

- Andar clicando no chão não bloqueia o jogo: dá para mudar de destino ou clicar num objeto no meio
  do caminho (no protótipo era preciso esperar chegar).
- Novo: controle pelo teclado (pedido do briefing), com destaque do objeto alcançável e números
  1–4 nos espaços do inventário.
- Estojo e apontador ainda não têm ilustração: aparecem como caixas "arte pendente", como no protótipo.

## Rig do Antônio (boneco recortado de perfil)

Quando anda de lado, o Antônio usa um boneco recortado em partes (cabeça, tronco, braços, pernas e braço
mecânico) com um ciclo de caminhada feito por código (`World/CutoutRig.cs`). As partes saíram do perfil da
folha de turnaround (`Design/personagens/antonio-turnaround.jpg`) com o script
`Design/tools/cortar_rig_antonio.py`, que também grava os pivôs das juntas em
`Assets/Resources/EP01/assets/characters/antonio-rig/rig.json`. A perna e o braço de trás são cópias mais
escuras dos da frente. Parado, ele continua usando a imagem de frente. Prévia das poses:
`Design/personagens/antonio-rig-poses.png`.

## Estado atual e limitações

- Feito: as 3 cenas jogáveis do começo ao fim, cutscene final, tela de fim com tempo de jogo, título
  com "Continuar", movimento em perspectiva com aceleração e animação procedural, NPCs com
  respiração/fala/inclinação, balões, inventário, dicas, caderno das contas e fade entre cenas.
- O código foi compilado contra as bibliotecas do Unity e o roteiro do JSON foi simulado de ponta a
  ponta, mas **ainda não foi aberto no Editor do Unity**. Na primeira vez pode aparecer algum ajuste
  fino de layout ou de importação. Se algo aparecer no Console, copie a mensagem para a gente corrigir.
- Ainda não tem som e o projeto usa o renderizador padrão (o URP 2D entra quando formos usar luzes 2D).

## Próximos passos sugeridos

1. Abrir no Editor, jogar o EP01 inteiro e anotar o que estranhar (tamanhos, velocidade, textos).
2. **Rig do Antônio** com o pacote 2D Animation (Antônio separado em partes: cabeça, tronco, braços,
   pernas, braço mecânico), substituindo a animação procedural.
3. Ilustrações do estojo e do apontador; versões dos cenários sem os objetos.
4. Sons (passos, apito, balões) e música.
5. Editor visual de hotspots: importar o JSON para ScriptableObjects e arrastar os retângulos na cena.
6. URP 2D e luzes (a luz rosa do final!), e build de teste para celular.
