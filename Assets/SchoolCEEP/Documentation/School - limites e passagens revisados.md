# Escola — limites e passagens revisados

A cena `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity` foi reconstruída, salva e reaberta. Esta revisão substitui as alturas e os acessos descritos na revisão anterior. Dimensões continuam estimadas pelas fotografias.

- A entrada possui quatro degraus baixos no lugar da rampa longa.
- As caixas da escada central e da escada do último bloco perto da quadra receberam paredes contínuas. As entradas e os patamares permanecem livres.
- A cantina tem passagem aberta de 1,80 m para uma pequena salinha de transição de 2,84 m, sob a cobertura existente. A folha e o vidro adjacente foram retirados. A janela da ligação com o bloco central foi substituída por alvenaria. A porta externa identificada na saída frontal da cantina, junto ao caminho da quadra, foi retirada e o vão fechado.
- O bloco laranja imediatamente junto à cantina recebe um pequeno desnível: seu piso está a −0,18 m e o corredor a +0,044 m. Uma soleira curta de concreto, com cerca de 1 m de comprimento e 4,05 m de largura, suaviza esse único degrau diretamente na boca da galeria. Não há corrimãos, patamares intermediários ou rampa longa lateral. Paredes, portas, janelas e mobiliário acompanham o piso.
- O piso do “hospício” fica a +0,60 m; as salas à frente ficam a −0,60 m. A aproximação desde o corredor principal é mais curta e sobe somente 0,60 m. A escada de oito degraus e os três lances entre os dois blocos continuam, com desnível total de 1,20 m. Os corrimãos da rampa foram retirados. A cobertura do corredor e os corrimãos da escada permanecem.
- O interior da Automotiva foi esvaziado. Seu portão é um elemento físico fechado, sem interação de abertura. O corredor coberto até o portão, o depósito e as cinco salas técnicas vizinhas permanecem.
- Muretas físicas de 1,25 m limitam jardins de cenário, o fundo da quadra e a saída do lote. Não atravessam as entradas ou o caminho principal.

## Verificação na cena salva

A auditoria aprovou 422/422 trajetos planejados e 462 caminhos físicos. Há 90 ambientes nomeados acessíveis; a Automotiva é a única área interior deliberadamente inacessível. Foram encontrados zero alvos inacessíveis nas áreas jogáveis, zero amostras de fechamento ausente, zero sobreposições entre paredes e portas fechadas e zero juntas suspeitas.

Os fechos das escadas passaram 1068 amostras físicas e 1068 amostras das faces renderizadas, preservando 40 degraus. A passagem da cantina passou testes de abertura e de suas paredes. A soleira curta passou 15 amostras distribuídas entre o centro e as duas bordas, incluindo os triângulos superiores realmente renderizados. As muretas passaram 180 amostras físicas, 180 amostras renderizadas e 30 tentativas de travessia com cápsula. Não restam objetos físicos no interior da Automotiva.

As portas restantes conservam o detalhamento com volume, batentes e maçanetas metálicas. Seus contatos passaram 600 verificações físicas e 600 verificações renderizadas. A otimização da alvenaria preserva o volume físico original. A cobertura inclinada da cantina e o recorte do teto adjacente à rampa permanecem.

As 29 vistas arquitetônicas foram renovadas e as vistas relevantes inspecionadas. A cena termina limpa, fora do Play, com uma câmera e com a iluminação noturna e a névoa restauradas. Os testes usam física nativa do Editor; não executam gameplay, perseguições, checkpoints ou medição de FPS.

As propostas de uso dos ambientes estão em `School - propostas de uso dos ambientes.md`. Elas não implementam automaticamente narrativa ou objetivos. Escritório, menu, áudio e progressão permanecem. Sem Meshy, GUI ou publicação.
