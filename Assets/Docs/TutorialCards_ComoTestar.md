# Dicas de item — ajustes e checklist

## Novo cartão em três passos

1. Create > Whispers > Tutorial card. Defina um id único, título, duas a quatro linhas de texto, controles, prioridade e ordem em Ajuda. Adicione o asset a TutorialCardCatalog.cards.
2. Escolha o gatilho: InventoryItem + ID do item; Signal + flag já existente; ou um evento explícito chamando GameSession.tutorials.Discover(id). Não renomeie IDs/flags existentes. Reutilize os gatilhos especiais Flashlight, WhistleLore, SecondEquipment e FirstMessage quando apropriado.
3. Escolha Model3D/Video/Image e referências. Use um prefab só visual para o modelo; vídeo deve ter imagem fallback e ser ligado como VideoClip. Teste em perfil descartável: evento, espera por momento seguro, fechar, Ajuda, salvar/carregar e modo desligado.

## Onde ajustar

Dados: Assets/HorrorGameplay/Data/TutorialCards/. Atraso padrão 0,75 s; prioridade maior abre primeiro. Depois de fechar um cartão, os pendentes esperam pelo menos 0,75 s, evitando sequência instantânea. O atraso só avança enquanto é seguro exibir. blocksGame=true congela o tempo; false mantém animações/tempo de simulação, mas a interface continua modal, sem controle do personagem. Os cinco cartões entregues usam true. Ajuda nunca retoma o jogo que já estava pausado.

Layout: TutorialCardView.Build/Layout. Canvas independente, 1920×1080, altura como referência, painel com escala limitada à tela; em 4:3 a mídia vai para o topo. Fonte Barlow Condensed local, tamanhos inteiros, Canvas pixelPerfect, imagens sem mipmaps/compressão e NPOT sem redimensionamento. Reduzir efeitos deixa só fade de 0,08 s; normal usa 0,22 s com escala de 0,985 para 1. Não altera a inspeção, PC ou celular existentes.

Pausa > Ajuda: mostra apenas mecânicas descobertas, inclusive as que foram suprimidas com as dicas desligadas; não revela o restante do catálogo. Pausa > Opções > Dicas de item: Completas / Só texto / Desligadas. O cartão não possui botão para desligar dicas. A opção permanece apenas em Opções. Nenhum cartão abre sozinho no modo Desligadas; Só texto não aloca câmera/modelo, VideoPlayer ou RenderTexture de mídia.

Bindings das ações expostas são consultados no InputReader.Actions e atualizam por evento de rebinding, inclusive com o cartão aberto. Bindings de gamepad são ignorados. Os gestos próprios do grampo (posição do mouse, clique esquerdo e Esc) seguem seu código existente, que não possui ações remapeáveis; o celular lê PhoneMessages.openKey do Inspector. Não foi criada tela de remapeamento nova. Desconhecidos mostram o nome da ação, conforme pedido.

## Gatilhos usados

| Cartão | Evento/condição | Prioridade |
|---|---|---:|
| flashlight | EquipmentController.CollectedFlashlight no capítulo do carro; aquisição observada nos equipamentos. Nunca no escritório. Fica pendente enquanto sentado. | 100 |
| whistle | Entrada na biblioteca pelo sinal existente bd.school.library; não é necessário ler o mural para receber a explicação. A hesitação e o desbloqueio narrativo do apito permanecem. | 110 |
| equipment | Evento de equipamento/inventário; dois itens equipáveis disponíveis. | 40 |
| lockpick | Inventory.Changed com o ID existente lockpick. Espera a fala/inspeção terminar. | 90 |
| phone | PhoneMessages.Changed, celular adquirido e primeira mensagem recebida. Espera banner sumir e janela fechar. | 70 |

## Checklist no Play

Use Novo Jogo ou cópia descartável. Em um save antigo, os itens/mecânicas já conhecidos são marcados como reconhecidos e não mostram cartão atrasado.

- Escritório: pegar equipamentos e usar PC/celular normalmente; nenhum cartão de lanterna ali.
- Carro: pegar a lanterna e sair. O cartão espera o assento/modal; ler, fechar com Entendi/Enter/Espaço/Esc, continuar. Verificar os glifos de slot, clique, roda e último item.
- Segundo item: cartão dos equipamentos; repetir número guarda o item, roda troca, Ajuda só mostra o que já foi descoberto.
- Biblioteca: entrar no cômodo; o cartão aparece após as falas, sem exigir leitura prévia do mural. Enquanto sem vídeo, há fallback identificado; ao ligar vídeo, Ver de novo reinicia a mídia.
- Secretaria: pegar o grampo. Cartão espera a reação de João; abrir minigame só depois de fechar. Conferir mouse/clique/Esc.
- Celular: vibrar, coletar e receber mensagem; cartão só depois do banner e fora da janela. Confirmar arraste pelo topo e bolinha vermelha.
- Abrir cada cartão durante teste de bateria baixa: bateria/relógio/eventos/telefone ficam parados; sons do mundo baixam suavemente. Ao fechar, volta ao estado anterior.
- Provocar eventos durante cutscene, inspeção, PC, celular, grampo, pausa e transição: fila aguarda. Nenhum cartão deve se sobrepor a diálogo importante.
- Só texto: sem mídia. Desligadas: sem automático, mas Ajuda continua. No cartão, verificar somente Entendi e Ver de novo quando houver animação.
- Remapear uma ação exposta no runtime (ou no sistema futuro de rebinding): o glifo muda; não confundir com gestos dedicados sem ação de input.
- Salvar/carregar: vistos, descobertos, fila e opção preservados; nenhum cartão visto abre sozinho novamente.
- Testar mouse, Tab/setas, Enter/Espaço/Esc; revisar tamanho em 16:9, 16:10, 4:3 e ultrawide.

Ajuda foi integrada à pausa. O menu principal permaneceu intacto, evitando leitura extra do save e spoilers fora da sessão.
