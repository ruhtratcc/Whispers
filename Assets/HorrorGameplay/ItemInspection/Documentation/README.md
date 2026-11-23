# Inspeção de itens — Whispers of the Past

Esta mecânica acrescenta inspeção 3D aos objetos da escola usando a interação existente. Menu, prólogo, planta da escola e eventos de progressão existentes permanecem fora desta mudança. A escola usa URP: uma câmera temporária renderiza somente a layer `Inspect`; sua Render Texture acompanha o formato da tela, com cerca de um megapixel no enquadramento normal e até 2048 pixels no lado maior ao aproximar.

O zoom em tela cheia foi integrado em 13/10/2026. A imagem cresce enquanto os controles desaparecem, e o arraste passa a mover a folha de perto. Veja o [guia desta etapa e suas provas](ZoomFullscreen.md). O painel **Ler texto** ainda aguarda o teste solicitado pelo usuário.

## Controles

- `E`: examinar o objeto; durante a inspeção, guardar.
- Botão esquerdo + arrastar: girar, com suavização.
- Scroll do mouse: aproximar ou afastar dentro dos limites.
- `F` ou **Virar**: mostrar o verso.
- `R` ou **Ouvir**: tocar a gravação, quando o objeto permite.
- `Esc`: voltar do zoom próximo; outro Esc fecha a inspeção. **Guardar** e `E` fecham diretamente.
- `I`: abrir as Anotações existentes; a faixa **Objetos para examinar** permite revisitar os itens salvos.

As dicas desaparecem após alguns segundos. O verso só apresenta sua transcrição quando está virado para a câmera; pequenos detalhes só aparecem ao aproximar. Em uma segunda inspeção, com sanidade baixa, uma transcrição alternativa pode aparecer.

O gravador apresenta `recordingText` na tela enquanto reproduz, independentemente do lado virado. O áudio desta integração é um ruído mecânico de fita; as falas narrativas aparecem como texto. Não há dublagem completa dos personagens.

## Como adicionar um objeto

1. Prepare um prefab **apenas visual**, leve, com o modelo centrado na origem, escala normalizada e a frente voltada para o eixo local `-Z`. Para papel, use espessura pequena e faces separadas, com materiais diferentes na frente e no verso. Evite usar a mesma face visível dos dois lados.
2. No Unity, escolha **Create → Whispers → Inspection item**. Preencha `itemId` com um identificador único que continuará igual nos saves, `title`, `description` e `model`. Para substituir texturas por esses campos, use um `MeshRenderer` na raiz do prefab com os materiais de frente e verso nos slots 0 e 1. Outros renderers podem ter as texturas atribuídas diretamente nos materiais do prefab.
3. Preencha `backText` com a transcrição do verso e `detailText` com o detalhe descoberto ao aproximar. `alternateBackText` é opcional. Texturas devem ter no máximo 2K.
4. Mantenha `recordInNotes` habilitado para permitir revisitar o objeto. Ajuste `stressPerSecond` e `maximumStress` somente para itens perturbadores; use zero em um item neutro. Configure `pickupSound`, `whisper` e, se necessário, `recording`, `recordingText` e `canPlay`. `recordingText` é a transcrição apresentada enquanto a gravação toca.
5. Acrescente esse asset à lista `items` do catálogo em `Assets/HorrorGameplay/Resources/SchoolInspectionItems.asset`. O gerenciador é instalado automaticamente ao entrar na escola, inclusive no Play direto do Editor; não é necessário salvar componentes novos no mapa. O catálogo permite localizar o modelo ao reabrir uma anotação ou carregar um save.
6. No objeto do mundo, mantenha um collider compatível com a interação atual e adicione `InspectableItem`. Arraste o asset para `item` e o objeto visual para `visual`. O objeto do mundo permanece na layer de interação habitual; a layer `Inspect` é reservada ao clone temporário da inspeção.
7. Para um objeto novo, use `InspectableItem` sozinho e deixe `originalInteraction` vazio. Se precisar disparar um evento ao concluir, configure `onInspectionFinished`. As pistas existentes dos tipos `SchoolOpeningClue`, `SchoolLeftBehindClue` e `SchoolMemoriesClue` recebem a integração automaticamente e preservam sua coleta e progressão originais. Esse encaminhamento não foi implementado para qualquer outro componente de interação: não preencha `originalInteraction` com um script diferente sem adaptar e testar seu fluxo.
8. Teste coleta, verso, zoom, saída e reabertura nas Anotações antes de ligar eventos de história.

## Teste da integração

Comece pela fotografia da turma. Confirme que ela entra suavemente, começa legível, gira e mostra **Alguns nunca foram embora.** somente no verso. Aumente o zoom para revelar o detalhe. Guarde: o controle deve voltar, a coleta deve ocorrer uma única vez e o objeto deve aparecer nas Anotações.

Depois confira o desenho, foto de João, relatório, chave e gravador. Abra e feche as Anotações repetidamente: não devem surgir botões duplicados. Clique em **Ouvir**, `R`, **Virar** e **Guardar**; clicar em um botão não deve arrastar o modelo. Navegue também pelos botões usando teclado, inclusive itens fora da área visível da lista.

Teste uma pausa, retomada, troca de capítulo e blackout com a inspeção aberta. Nenhum desses caminhos pode deixar o jogador sem controles, a câmera temporária ativa ou a luz da inspeção iluminando o mapa. Carregue um save e confirme que as inspeções e o marcador de item novo continuam coerentes.

## Validação concluída

Os três testes nativos no Unity passaram, cada um com um caso e nenhuma falha:

| Categoria | Resultado | Tempo | Verificação |
|---|---|---|---|
| ItemInspection | 1/1 passou | 5,31 s | Restauração de controles, coleta única, reabertura e interrupção por blackout |
| SchoolOpeningScene | 1/1 passou | 17,20 s | Portão, pistas, linhas de visão e blackout na abertura da escola |
| ItemInspectionInput | 1/1 passou | 16,61 s | Teclado, mouse e saída da inspeção sem abrir a pausa |

Relatórios: [tests-base.json](tests-base.json), [tests-opening.json](tests-opening.json) e [tests-input.json](tests-input.json).

A captura da câmera de inspeção concluiu 14 solicitações, cobrindo nove objetos dos seis tipos iniciais: fotografia da turma, desenho, foto de João, documentos, chave e gravador. O relatório [Visual/inspection.json](Visual/inspection.json) registra `complete: true`, `restored: true` e `errors: []`. As imagens estão na mesma pasta. Isso confirma a captura e a restauração de estado; não é uma medição de desempenho.

As 14 imagens foram revisadas visualmente. As capturas isoladas usam a câmera real da inspeção; as duas composições de UI adaptam temporariamente os canvases para a captura. Elas servem para conferir enquadramento e legibilidade, sem representar um benchmark do player final.

A verificação [protected-assets-final.json](protected-assets-final.json) confirma os cinco arquivos protegidos sem alterações: menu, prólogo, viagem, escola e roteiro. O guard de testes restaurou os arquivos de checkpoint e preferências em [test-guard-restored-final.json](test-guard-restored-final.json). A auditoria final encontrou uma câmera, jogador e sessão, com zero scripts, meshes ou materiais ausentes e nenhum erro de narrativa ou shader. Os nove prefabs são visuais, sem componentes de física ou gameplay; as duas texturas importadas são 1K. Relatórios: [final-SchoolCEEP.json](final-SchoolCEEP.json) e [final-assets.json](final-assets.json).

Não foi refeita a interface antiga de Anotações: a faixa de objetos reserva espaço dentro do painel somente quando necessária, reutiliza seus botões e devolve o espaço ao fechar. Não há pool, dependência nova ou câmera de inspeção permanente.

Não foram medidos o FPS nem uma build de distribuição. Os testes automatizados usam mudanças diretas de posição entre locais e não equivalem a percorrer a campanha inteira manualmente. As gravações mantêm ruído de fita e transcrição, sem dublagem completa.
