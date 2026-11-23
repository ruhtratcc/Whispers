# Whispers of Past — apresentação e atmosfera

Revisão solicitada em 01/10/2026. Mantém o projeto existente, o menu em uGUI, o sistema de saves e as interações do escritório. Nenhum modelo Meshy foi usado.

## Menu e história preparada

O escritório passa a ser apresentado como **Prólogo — O Telefone**. É a única entrada disponível, carregando a cena existente `Chapter1Office`. A apresentação descreve João e a ligação que iniciará a investigação; não executa diálogos ou ligações nesta etapa.

A lista preparada segue o roteiro, com a renumeração após o prólogo:

1. O Caminho até a Escola.
2. A Escola Abandonada.
3. O Que Foi Deixado para Trás.
4. As Memórias.
5. O Terraço.
6. A Última Saída.

Essas seis entradas permanecem bloqueadas e sem cenas de destino. A duração fictícia anterior é removida. O título continua **Whispers of Past**, conforme o nome atual aprovado pelo usuário. O documento original permanece intacto.

O menu recebe tipografia mais legível, melhor espaçamento e contraste, foco dos botões e hierarquia de opções. A granulação contínua diminui; o risco de câmera antiga, os olhos discretos, a porta esquerda e a névoa no corredor permanecem. Letras ficam acima do filtro, com desgaste e instabilidade discretos no título.

A iluminação do menu usa dois mapas de sombra: luminária principal e fresta da porta esquerda. As luzes pontuais de preenchimento não geram sombras. Antes, três luzes Point mais uma Spot reservavam 19 faces no atlas; a redução mantém o atlas existente.

A lâmpada do corredor recebe um tubo emissivo com brilho suave. O Spot fica logo abaixo do tubo e ilumina o piso sem ficar preso na carcaça. Intensidade 14 também é registrada no componente que controla sua oscilação, evitando perder o ajuste ao entrar em Play. Bloom suave de .14 destaca a lâmpada; o orçamento continua em dois mapas de sombra.

## Idioma

Menu e pausa apresentam **Português** e **English**. English altera somente o seletor e o aviso de disponibilidade futura. Textos continuam em português. A escolha decorativa usa `HorrorMenu.LanguagePreview`; não há pacote de localização nem tradução simulada.

## HUD

Interação separa tecla e ação; mira tem resposta suave; stamina continua oculta quando cheia; bateria recebe ícone e estado. Objetivos, legendas, documentos, inventário, pausa e opções mantêm suas funções com melhor apresentação.

Áudio, sensibilidade, saves, cursor, pausa e retorno ao menu permanecem conectados aos componentes existentes. Chiado e respiração provisória continuam desligados. Lanterna mantém intensidade 90 e começa desligada no escritório.

Os puxadores dos controles de volume e sensibilidade ficam separados, sem sobreposição entre linhas. A verificação nativa mede a altura efetiva de cada puxador nos três alvos: prefab, laboratório e escritório.

Os botões do menu principal e Voltar das opções usam o texto como sua área de clique. O passe preserva `raycastTarget=true` nesses textos e mantém os textos decorativos sem raycast. A correção foi salva e verificada pelo GraphicRaycaster real: dez resultados positivos, cobrindo cinco botões em 16:9 e 4:3. O relatório é `Assets/HorrorMenu/Documentation/Menu-pointer-fix-report.json`; a prova confere a chegada do ponteiro ao Button e não executa Sair nem carrega cenas.

## Escritório

Sombras em três luzes Spot: luminária de mesa, teto e janela. Pontos de preenchimento não geram sombras. Atlas e resolução da URP existente são preservados quando já compatíveis. Uma luz pontual com sombras exige seis mapas, enquanto uma Spot usa um; a configuração limita o custo, mas não garante uma taxa de FPS em qualquer GPU. [Documentação Unity](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/shadows-in-urp/shadow-resolution-urp).

A iluminação geral usa branco quente neutro. A fonte do teto fica 8 cm abaixo do globo, com intensidade 18, alcance 11 m e cone amplo. O preenchimento existente é centralizado sob a luminária, com intensidade 7,5 e alcance 11 m; distribui luz em paredes, estantes e cantos sem acrescentar sombras. A mesa e a rua mantêm suas fontes próprias. As prévias do HUD também desativam a lanterna temporariamente, para mostrar a iluminação real de início do prólogo.

Rua básica visível pela janela: asfalto, calçada, fachada e poste. É um fundo visual sem colisores ou área externa jogável. A janela mantém seu limite de colisão.

A correção adicional alinha a luz que entra pela janela, o halo e a iluminação da rua ao bulbo visível do poste. O bulbo recebe emissão; um Spot sem sombras clareia a pista e a fachada sem acrescentar outro mapa de sombra.

Um interruptor físico fica ao lado da entrada. Pelo pedido mais recente, teto e preenchimento permanecem **sempre ligados**; o interruptor fica visual, sem prompt de interação. Luminária de mesa, monitor e iluminação externa permanecem independentes. O componente reutilizável `InteractableLightSwitch` usa `alwaysOn=true` somente neste circuito: até chamadas `SetState(false)` conservam a luz acesa. Em outros cenários, `alwaysOn=false` mantém sua interação normal. Não há evento narrativo ligado a ele.

A roupa do cabideiro é substituída por um chapéu de investigador com aba curva, copa, faixa e acabamento. Telefone, gaveta, mesa, computador, cadeiras, relógio e arquivos mantêm as correções anteriores.

## Aplicação e backup

O importador revisável fica em `work/PresentationPolish/ApplyPresentationPolish.cs`. Usa um callback único de `EditorApplication.update`, permitindo retornar antes do limite de 5 segundos do Pipeline. Após importar e compilar, o bootstrap executa os passes por etapas, somente com o pedido explícito `Presentation-polish.request`. Reloads normais não reaplicam a revisão.

Há backup das cenas, prefabs, materiais, assets e scripts existentes em `work/PresentationPolish/Backups`. Arquivos meta e GUIDs existentes são preservados. Os passes editam cenas e assets por APIs do Editor.

## Verificação

A compilação offline verifica fontes exatas e APIs da versão instalada. O importador gera prévias do menu em 16:9 e 4:3, HUD/pausa/opções, escritório, janela, chapéu e comparação com/sem sombras. Verificações nativas conferem referências, capítulos bloqueados, sombras, colisão da janela e IDs persistentes.

Os testes anteriores de gameplay são repetidos, com a fixture da porta corrigida para aguardar movimento observável após remover o bloqueio. Não há mudança no comportamento da porta por causa dessa correção de teste.

Na aplicação inicial de 01/10/2026, os relatórios nativos passaram e os testes concluíram **5/5 EditMode e 14/14 PlayMode**, sem falhas. O usuário posteriormente dispensou novos testes da porta e salinha; a correção adicional de apresentação não os repete. A tentativa de desligar o circuito fixo é conferida diretamente no Editor, exigindo que as luzes continuem acesas.

Relatórios em `Assets/ChapterOneOffice/Documentation`: `Presentation-polish-apply.json`, `HUD-presentation-report.json`, `Office-atmosphere-polish-report.json` e `Office-polish-tests.json`/XMLs. O relatório do menu fica em `Assets/HorrorMenu/Documentation/Menu-presentation-report.json`.

**Estado:** todas as alterações aplicadas e salvas no projeto. Verificação final em 01/10/2026 às 15:21 UTC: `Applied; verified`, erro vazio, menu/HUD/escritório aprovados. Quinze prévias reais foram conferidas, incluindo a sala e o HUD com a lanterna desligada. O teto e preenchimento permanecem ligados; os controles de volume aparecem separados. A aferição do HUD ativa temporariamente o painel para medir o layout visível e restaura seu estado, sem mudar preferências. Nenhuma build Windows solicitada nesta revisão.
