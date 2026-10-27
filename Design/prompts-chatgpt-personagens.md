# Prompts para redesenhar os personagens no ChatGPT

Use **uma conversa só** no ChatGPT, na ordem abaixo: cada passo usa a imagem do passo anterior como referência.
Se uma imagem vier errada (tamanhos diferentes, texto escrito, fundo branco), peça de novo antes de seguir.
Salve cada resultado em `Design/personagens/v4/` com o nome indicado.

---

## Passo 1 — Elenco em escala (`elenco-escala.png`)

Anexe: a sala (`Assets/Resources/EP01/assets/scenes/ep01-sala-2d.png`), `Design/personagens/antonio-folha-poses-v3.png`
e as imagens da Lúcia, do Caio, da Bia e do Beto (`Assets/Resources/EP01/assets/characters/npc-*.png`).

```
Vamos redesenhar os personagens de um jogo 2D infantil (aventura point-and-click para crianças, estilo livro ilustrado).
Estilo: igual ao cenário da sala de aula anexado — lápis de cor com textura de papel, contorno marrom-escuro levemente irregular, cores quentes e suaves, sombras leves. Nada de 3D, nada de brilho de plástico.

Crie uma FICHA DE ELENCO EM ESCALA, com fundo 100% transparente (PNG com canal alfa), sem nenhum texto, número ou régua.
Todos de FRENTE, em pé, braços soltos ao lado do corpo, lado a lado com espaço entre eles, todos com os pés na MESMA linha de base invisível e na MESMA escala (como uma foto de grupo de altura real):

1. Antônio, 8 anos, 1,28 m — use o menino da folha anexada (cabelo castanho em cuia, camiseta branca com escudo, manga azul, short azul-marinho, meias brancas, tênis vermelhos).
2. Caio, 8 anos, colega da mesma turma, 1,32 m — use o Caio anexado.
3. Bia, 8 anos, 1,26 m — use a Bia anexada.
4. Beto, use o Beto anexado, mantendo a idade e a altura que ele aparenta.
5. Professora Lúcia, adulta, 1,65 m — use a Lúcia anexada.

Proporções (muito importante): as crianças têm cerca de 4,5 cabeças de altura (cabeça grande e simpática, mas não chibi); a professora tem cerca de 7 cabeças. O topo da cabeça do Antônio fica na altura do peito da professora.
```

## Passo 2 — Antônio de todos os lados (`antonio-modelo.png`)

```
Agora só o Antônio, exatamente como na ficha de elenco (mesmo traço, mesmas cores, mesmas proporções de 4,5 cabeças).
Fundo 100% transparente, sem texto. 4 vistas lado a lado, todas com EXATAMENTE a mesma altura e os pés na mesma linha de base:
frente, três-quartos virado para a direita, perfil olhando para a direita, costas.
Em pé, postura relaxada, braços soltos ao lado do corpo, mãos abertas e relaxadas.
Ele NÃO segura nada.
```

## Passo 3 — Peças para animar a caminhada (`antonio-pecas-perfil.png`)

É daqui que sai a caminhada: no Unity o boneco é montado com essas peças e anda por código, sempre no mesmo
tamanho, sem trocar de desenho.

```
Agora prepare o Antônio de PERFIL (olhando para a direita, igual à vista de perfil anterior) como um BONECO DE PAPEL ARTICULADO para animação 2D (cut-out / paper doll).
Fundo 100% transparente, sem texto. Desenhe cada peça SEPARADA, solta, com bastante espaço entre elas, todas na mesma escala do perfil:

- cabeça (com o cabelo e a orelha; o pescoço fica no tronco)
- tronco com o pescoço e a camiseta (sem os braços)
- short (quadril)
- braço da frente: parte de cima (manga) e, separado, antebraço com a mão
- braço de trás: mesmas 2 peças, um pouco mais escuras
- perna da frente: coxa, canela com a meia e, separado, o pé com o tênis
- perna de trás: mesmas 3 peças, um pouco mais escuras

Regras das peças:
- cada ponta que encaixa em outra peça (ombro, cotovelo, quadril, joelho, tornozelo) termina ARREDONDADA, como um círculo, e um pouco mais comprida, para sobrepor na junta sem abrir buraco quando dobrar;
- braços e pernas desenhados RETOS, apontando para baixo;
- nenhuma peça cobre a outra; nada cortado na borda da imagem.

Ao lado, desenhe também o boneco MONTADO com essas mesmas peças, em pé, para conferir.
```

## Passo 4 — O braço mecânico sozinho (`braco-mecanico.png`)

```
Desenhe só o braço mecânico de brinquedo do Antônio (pegador amarelo com cabo e garra de duas pontas), no mesmo estilo, sem o menino.
Fundo 100% transparente, sem texto. 2 versões lado a lado: garra aberta e garra fechada.
Cabo na vertical, garra para baixo. Tamanho em relação ao Antônio: o cabo vai da mão dele até a altura do joelho.
```

## Passo 5 — Poses de interação (`antonio-poses.png`)

```
Agora uma folha de POSES do Antônio, exatamente como na ficha de modelo (mesmo traço, mesmas proporções, mesmo tamanho de cabeça em todas).
Fundo 100% transparente, sem texto, 4 colunas x 3 linhas, com bastante espaço entre as figuras.
Todas na MESMA escala: as poses em pé têm exatamente a mesma altura da vista de frente do modelo.
Ele NÃO segura nada; a mão direita fica FECHADA, como quem segura um cabo (vamos colocar o braço mecânico nela depois).

1. frente, parado, sorrindo
2. frente, olhos fechados (piscando) — igual à 1, só os olhos mudam
3. frente, falando, boca aberta — igual à 1, só a boca muda
4. três-quartos, olhando para quem está ao lado
5. perfil, olhando para cima
6. perfil, olhando para baixo
7. perfil, agachado, pegando algo no chão
8. costas, olhando para o quadro
9. perfil, sentado numa cadeira (desenhe só o menino, sem a cadeira)
10. perfil, agachado preparando o pulo
11. perfil, no ar, pulando, braços para cima
12. perfil, chegando ao chão do pulo, joelhos dobrados
```
