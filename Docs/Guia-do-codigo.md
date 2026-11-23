# Whispers of the Past | Código explicado

## O jogo em seis partes

| Parte | O que o código faz | Onde começar |
| --- | --- | --- |
| Menu e início | Abre um jogo novo, mostra a introdução ou carrega o checkpoint. | `MainMenuController`, `SceneFader` |
| Capítulo 1 · Escritório | Processa os emails e libera as próximas interações do escritório, como a gaveta e a saída. | `BeforeDemolitionOfficeFlow`, `RetroComputer` |
| Capítulo 2 · Estrada | Move o carro, sincroniza o rádio e libera as ações da chegada na ordem da história. | `BeforeDemolitionRoadFlow`, `BeforeDemolitionRoadRadio` |
| Capítulos 3 e 4 · Escola | Registra evidências da secretaria e biblioteca. Depois acompanha os quatro alunos, a fita e a chave da escada. | `BeforeDemolitionSchoolFlow`, `BeforeDemolitionFourStudentsFlow` |
| Capítulo 5 · Duas Vozes | Repete os módulos do corredor, muda os sons e apresenta a rota de saída. | `BeforeDemolitionTwoVoicesFlow`, `CorridorAtmosphere` |
| Capítulo 6 · Espelho e desfecho | Apresenta a escolha, executa a consequência correspondente e coordena o epílogo. | `BeforeDemolitionMirrorFlow`, `EndingEpilogue` |

## Como as partes se ligam

- **Controle.** `InputReader` recebe o comando do teclado, mouse ou controle. `GameSession` verifica se é hora de explorar, pausar ou inspecionar. O motor movimenta João, e a câmera cuida da visão.
- **Interação.** `PlayerInteraction` lança um raio à frente da câmera para identificar o alvo. Cada objeto decide o que significa interagir com ele, como abrir uma gaveta ou examinar uma fita.
- **História.** O `StoryBook` é a lista de capítulos, falas e objetivos. O `NarrativeProgress` registra o que já aconteceu. O `NarrativeDirector` confere se a próxima ação está na ordem certa e avisa o resto do jogo.
- **Inventário.** Quando um item entra no `Inventory`, o aviso entra na fila e o item passa a aparecer no Arquivo Pessoal. O arquivo usa os mesmos dados da inspeção 3D para reabrir o registro.
- **Salvamento.** O `SaveSystem` grava o progresso e os estados dos objetos por ID. Ao carregar, pode recorrer à cópia de segurança. O ID continua o mesmo mesmo que a posição do objeto na cena mude.
- **Atmosfera.** Sons podem ter posição e distância. Luz, áudio e efeitos de imagem dão sinais da tensão e da sanidade. O corredor e o espelho têm apresentações próprias.
- **Unity.** A cena liga os componentes aos objetos físicos. Os arquivos `.asset` guardam valores e conteúdo editável. Os shaders descrevem como certos pixels devem aparecer. Os scripts de Editor montam e verificam as cenas usadas no jogo.

**Exemplo.** João aperta o botão de interação ao olhar para a gaveta. O raio de `PlayerInteraction` encontra `InteractiveDrawer`. Se a sequência da história já liberou a gaveta, ela abre. Quando o item dentro dela é coletado, `Inventory` registra o ID e o `NarrativeDirector` avança o objetivo.

## O que foi simplificado

- Em 54 scripts densos, reorganizei espaços, quebras de linha e recuos. O compilador C# confirmou que os tokens e comentários são os mesmos. Isso torna mais fácil acompanhar condições, métodos e blocos.
- `StoryBook.cs` usa a mesma busca de capítulo para obter a posição ou a definição inteira.
- `NarrativeProgress.cs` usa uma busca única para encontrar o próximo marco e verifica os itens obrigatórios em ordem, parando no primeiro que faltar.
- `FootstepLibrary.cs` guarda a primeira opção disponível enquanto procura a superfície exata. As prioridades de material, tag e som padrão continuam como estavam.

**Verificação.** Comparei os mesmos 300 testes de Edit Mode antes e depois. Os 299 aprovados e a falha antiga da contagem de falas do apito foram idênticos. Três testes em Play Mode passaram, cobrindo a introdução, o Arquivo Pessoal e a passagem do espelho. Os testes usaram uma cópia do projeto. A campanha inteira e um executável novo ficaram fora desta verificação.

[Índice dos 432 scripts C# compilados](Indice-dos-scripts.txt)

## Código visual e ferramentas

| Arquivo | O que faz |
| --- | --- |
| `VintageCamera.shader` | Aplica granulação, cor e interferências do efeito de fita antiga. |
| `RetroDesktopCRT.shader` | Dá à tela do computador a aparência de um monitor CRT. |
| `MirrorReflectionMask.shader` | Marca os limites do vidro usados para recortar o reflexo. |
| `MirrorReflectionActor.shader` | Renderiza a figura vista dentro do espelho. |
| `WhistlePulse.shader` | Apresenta o pulso visual do toque. |
| `WhistleOutline.shader` | Mostra os contornos dos alvos que respondem ao apito. |
| `WhistleTraces.shader` | Exibe os rastros que podem orientar a investigação. |
| `Tools/Audio/sfx_pipeline.py` | Automatiza etapas do preparo de efeitos sonoros. |
| `Tools/Audio/test_sfx_pipeline.py` | Testa a ferramenta de preparação dos efeitos. |

O índice traz também os scripts de ferramentas do Editor, principalmente os que montam e revisam a escola, além dos testes. Cada teste indica os nomes dos cenários que ele verifica.

## Ícone do jogo

A porta entreaberta foi escolhida para representar a entrada de João na escola e continuar legível quando reduzida para o ícone do Windows. O PNG está configurado nos tamanhos de desktop da Unity. O ICO avulso contém tamanhos de 16 a 256 pixels. A imagem foi gerada com a ferramenta integrada, usando a descrição de uma porta entreaberta geométrica, em marfim sobre fundo carvão, sem texto.


## Scripts de execução

### Áudio

- `AudioCueLibrary` · Localiza efeitos sonoros pelos nomes usados nos scripts.
- `AudioManager` · Toca e reaproveita fontes de som, ajustando volume, posição e canais do mixer.
- `BreathingResponse` · Calcula a intensidade da respiração conforme esforço, medo e pausa.
- `DemonVoice` · Escolhe e mistura as camadas da voz de Helena no corredor e no espelho.
- `DemonVoiceConfig` · Guarda clipes, volumes e atrasos das camadas da voz.
- `FootstepAudio` · Dispara passos e aterrissagens quando João se movimenta.
- `FootstepLibrary` · Escolhe o som do piso pelo tipo de superfície, material físico ou tag.
- `ImpactAudio` · Toca o efeito de impacto de objetos físicos.
- `RandomScareAudio` · Escolhe quando e onde reproduzir sons inesperados.
- `SceneAudioDetails` · Aplica detalhes sonoros próprios de cada cenário.
- `SfxCatalog` · Reúne e localiza os efeitos que o jogo usa.
- `SoundSet` · Agrupa variações de um som e evita repetir o último clipe quando há alternativas.
- `SpatialAmbience` · Posiciona e regula ambientes sonoros no espaço.
- `SurfaceType` · Identifica o material do chão para os passos.
- `VoiceAudioPresentation` · Aplica o tratamento de voz de rádio, interfone ou ambiente.

### PC do escritório

- `DesktopBootView` · Mostra a sequência de ligar o computador.
- `DesktopContent` · Guarda o conteúdo, as imagens e os dados dos aplicativos.
- `DesktopContentDefaults` · Define o conteúdo padrão do computador fictício.
- `DesktopError` · Monta as caixas de erro com aparência do sistema.
- `DesktopInvestigationApps` · Controla correio, documentos, imagens e lixeira.
- `DesktopJungBrowser` · Exibe páginas locais da investigação no navegador fictício.
- `DesktopNote` · Organiza as anotações do computador.
- `DesktopPage` · Define uma página que o navegador pode abrir.
- `DesktopPointer` · Transforma a interação do jogador em cursor na tela do monitor.
- `DesktopSecondaryApps` · Implementa Bloco de Notas, Media Player e Campo Minado.
- `DesktopSnakeApp` · Controla a Cobrinha, suas entradas e o estado da partida.
- `DesktopStartMenu` · Abre os aplicativos pelo menu Iniciar.
- `DesktopSystemText` · Centraliza textos usados pelo computador fictício.
- `DesktopUI` · Monta a área de trabalho e coordena aplicativos e janelas.
- `DesktopWindow` · Gerencia posição, foco e tamanho das janelas.
- `RetroComputer` · Coordena a câmera, o cursor, o monitor e o fluxo do computador.
- `RetroComputerInteraction` · Encaminha o comando de usar o computador.

### Correio

- `EmailCatalog` · Contém o texto, os anexos e as regras iniciais de entrega dos emails.
- `EmailInboxController` · Entrega, registra leituras e comunica mudanças ao jogo.
- `EmailInboxState` · Valida e guarda os IDs dos emails entregues e lidos.
- `EmailInboxView` · Exibe a caixa de entrada e o conteúdo das mensagens.
- `RoadPhoneCallView` · Apresenta a interface da ligação recebida na estrada.

### Equipamento

- `EquipmentController` · Controla o item da mão, a troca de slot e o uso do apito ou da lanterna.
- `EquipmentDefinition` · Define os itens equipáveis e as preferências gravadas desse sistema.
- `EquipmentHud` · Mostra o item, a carga e o tempo de espera quando necessário.
- `EquipmentOptionsPanel` · Exibe as opções de HUD, lanterna e rolagem da roda do mouse.

### Inspeção e arquivo

- `ArchiveRecords` · Classifica documentos, itens e pistas pelo registro conhecido.
- `DiscoveryNotifications` · Mostra os novos registros em fila, com miniatura e som quando disponíveis.
- `InspectableItem` · Permite que um objeto da cena entre no sistema de inspeção.
- `InspectionCameraController` · Posiciona a câmera e controla giro e zoom.
- `InspectionItemData` · Guarda modelo, textos, imagens, páginas e gravação de um objeto.
- `InspectionManager` · Abre e fecha a inspeção, gerencia o item atual e confirma ações de coleta.
- `InspectionUI` · Exibe controles, páginas, leitura e gravações durante a inspeção.
- `NarrativeArchive` · Mostra e reabre os registros que João já coletou.
- `SchoolInspectionCatalog` · Localiza os dados de inspeção pelo ID do item.

### Interações

- `CollectibleItem` · Processa a coleta de itens e registra a ação no inventário.
- `HidingSpot` · Controla a entrada e a saída dos lugares em que é possível se esconder.
- `IInteractable` · Define o contrato comum de uma interação, como mostrar a dica e executar a ação.
- `InteractableLightSwitch` · Liga ou desliga luzes ligadas ao interruptor.
- `InteractiveDoor` · Controla abertura, fechamento, trava e estado persistente de portas.
- `InteractiveDrawer` · Controla o movimento, a interação e o estado de uma gaveta.
- `Inventory` · Guarda os itens adquiridos e registra quais foram examinados.
- `PhysicsGrabber` · Pega, segura e solta objetos físicos.
- `PhysicsPickup` · Define os objetos que podem ser levantados.
- `PlayerInteraction` · Identifica o alvo à frente da câmera e encaminha a ação escolhida.
- `SchoolAreaRestriction` · Restringe interações ou áreas que estão fora da rota configurada.

### Fechaduras

- `LockableDoor` · Conecta fechaduras e seus estados a portas ou gavetas.
- `LockpickConfig` · Guarda a dificuldade e os parâmetros de arrombamento.
- `LockpickDialogue` · Fornece as falas associadas ao uso do grampo.
- `LockpickNoteProposal` · Organiza o texto de apoio sobre fechaduras e o grampo.
- `LockpickPickup` · Registra a coleta do grampo.
- `LockpickSystem` · Executa o minijogo e controla os pinos da fechadura.
- `LockpickView` · Desenha a interface da fechadura e seus controles.
- `PinGlint` · Controla o brilho usado como indicação do grampo.

### Campanha e narrativa

- `BeforeDemolitionCampaign` · Identifica a campanha atual, seus capítulos e as regras de compatibilidade de progresso.
- `BeforeDemolitionContent` · Reúne as definições de conteúdo da campanha atual.
- `BeforeDemolitionEndingView` · Mostra a decisão final e as telas associadas ao desfecho.
- `BeforeDemolitionFourStudentsFlow` · Coordena os quatro alunos, os itens, a gravação de Bia e o acesso à escada.
- `BeforeDemolitionFourStudentsInteraction` · Liga os objetos dos quatro alunos ao fluxo do capítulo.
- `BeforeDemolitionMirrorFlow` · Controla a apresentação do espelho, as escolhas e o retorno do apito.
- `BeforeDemolitionOfficeFlow` · Coordena o PC, os emails, a gaveta e a saída do escritório.
- `BeforeDemolitionOfficeInteraction` · Liga portas e objetos do escritório às ações narrativas.
- `BeforeDemolitionRoadFlow` · Coordena a viagem, as ações dentro do carro e a entrada na escola.
- `BeforeDemolitionRoadInteraction` · Traduz interações com o interior do carro e o portão em ações.
- `BeforeDemolitionRoadRadio` · Troca rádio, noticiário, músicas, chamadas e a cantiga.
- `BeforeDemolitionRoadState` · Guarda a etapa do rádio e as ações já concluídas na estrada.
- `BeforeDemolitionSchoolFlow` · Coordena a secretaria, as evidências, a chave e a visita à biblioteca.
- `BeforeDemolitionSchoolInteraction` · Traduz interações com os objetos da secretaria e da biblioteca em ações.
- `BeforeDemolitionSchoolState` · Guarda o progresso e os estados usados na primeira visita à escola.
- `BeforeDemolitionTwoVoicesFlow` · Coordena o corredor infinito, a fita e a saída para o espelho.
- `BeforeDemolitionWhistle` · Integra o apito às regras de investigação da campanha.
- `ChapterTransitionView` · Mostra os cartões e fades das trocas de capítulo.
- `CorridorAtmosphere` · Monta a apresentação do corredor infinito, com névoa, luz e marcas.
- `CorridorAtmosphereConfig` · Guarda medidas, limites e parâmetros visuais do corredor.
- `EndingEpilogue` · Orquestra a sequência final, incluindo transições, notícias e apresentação das cenas.
- `EndingPresentationConfig` · Reúne textos, imagens, falas e durações do final.
- `GhostAppearance` · Controla aparições guiadas da versão anterior da história.
- `MirrorReflectionMask` · Recorta a figura do reflexo para aparecer dentro do vidro.
- `NarrativeDirector` · Aceita eventos, atualiza o objetivo e coordena diálogos e capítulos.
- `NarrativeProgress` · Guarda os eventos cumpridos e verifica qual ação vem em seguida.
- `OfficeEndingFlow` · Coordena o retorno ao escritório no fluxo narrativo ao qual está ligado.
- `OfficeWardrobe` · Controla a interação com o armário do escritório.
- `PoltergeistEvent` · Encena mudanças pontuais no cenário a partir de eventos configurados.
- `PrologueFlow` · Coordena o prólogo alternativo do escritório.
- `RoadJourneyFlow` · Controla a viagem da versão anterior da história.
- `RoadJourneyInteraction` · Adapta os pontos interativos à viagem anterior.
- `SchoolFinalPhone` · Executa a ligação final do fluxo anterior.
- `SchoolLastExitFlow` · Coordena a última saída na versão anterior.
- `SchoolLeftBehindClue` · Identifica as pistas do capítulo antigo sobre o que ficou para trás.
- `SchoolLeftBehindFlow` · Executa a investigação da área antiga no roteiro anterior.
- `SchoolMemoriesClue` · Identifica uma pista do capítulo anterior de memórias.
- `SchoolMemoriesFlow` · Controla as descobertas e transições das memórias no roteiro anterior.
- `SchoolMemoryMirror` · Controla a interação com o espelho de memórias da versão anterior.
- `SchoolOpeningClue` · Identifica um objeto da abertura antiga da escola.
- `SchoolOpeningFlow` · Coordena os primeiros eventos no roteiro anterior da escola.
- `SchoolSimplifiedFlow` · Coordena a campanha simplificada usada como roteiro anterior.
- `SchoolTerraceFlow` · Controla a subida e as revelações do antigo capítulo do terraço.
- `SecretaryEvidence` · Controla a análise das pegadas na secretaria.
- `SecretaryEvidenceText` · Sincroniza os textos das evidências entre versões de save.
- `SimplifiedSchoolClue` · Registra pistas da campanha simplificada.
- `SimplifiedStoryContent` · Monta as definições e itens do roteiro simplificado.
- `StoryBook` · Reúne capítulos, marcos, falas e sequências, com buscas por ID.
- `StoryContent` · Reúne o conteúdo e os padrões da campanha anterior.
- `StoryEquipment` · Permite coletar equipamentos ligados à história.
- `StoryExit` · Processa pontos de saída vinculados à narrativa.
- `StoryInteraction` · Liga uma interação genérica a um evento narrativo.
- `StoryTelephone` · Liga o telefone do cenário a uma ação narrativa.
- `StoryWorldEvent` · Liga eventos no ambiente a marcos da história.

### Objetivos e celular

- `ChapterObjectives` · Guarda textos e etapas consultáveis dos objetivos.
- `ObjectiveOptions` · Controla preferências sobre a apresentação do objetivo.
- `ObjectiveService` · Atualiza a tarefa atual conforme o capítulo e o progresso.
- `PhoneMessageCatalog` · Guarda mensagens, fotos e gatilhos de entrega do celular.
- `PhoneMessages` · Gerencia entrega, notificações, leitura e salvamento das conversas.
- `PhoneMessagesView` · Monta a interface das conversas do celular.
- `PhoneProgression` · Verifica leituras obrigatórias, lembretes e travas ligadas às mensagens.
- `PhoneProgressionConfig` · Define as mensagens e áreas usadas para avançar a história.
- `PhoneWindowDrag` · Permite arrastar uma janela de conversa.

### Jogador, efeitos e sistemas

- `FirstPersonCamera` · Posiciona a câmera, aplica balanço e responde aos controles de visão.
- `FirstPersonMotor` · Executa movimento, corrida, agachamento, salto e colisões do jogador.
- `FlashlightController` · Controla feixe, bateria e aparência da lanterna na mão.
- `VintageCameraEffect` · Configura o filtro de vídeo antigo conforme o capítulo e a tensão.
- `VintageCameraRendererFeature` · Aplica o filtro de imagem no pipeline URP.
- `Checkpoint` · Marca um lugar ou evento que serve como ponto de retorno.
- `ContextBarks` · Dispara comentários curtos e contextuais de João.
- `DarknessZone` · Identifica regiões escuras para o sistema de atmosfera.
- `EventZone` · Detecta entrada em áreas que disparam uma ação.
- `FlickerLight` · Executa oscilações de luz configuradas no cenário.
- `GameClock` · Mantém o relógio interno usado nas cenas e interfaces.
- `GameSession` · Coordena os modos de jogo como exploração, pausa, inspeção e cutscene.
- `HorrorAtmosphere` · Integra sinais de tensão e ajustes atmosféricos.
- `InputReader` · Lê teclado, mouse e controle e entrega os comandos aos sistemas.
- `Jumpscare` · Executa sustos pontuais configurados no cenário.
- `LoadingMetrics` · Mede etapas de carregamento para diagnóstico.
- `NoiseSystem` · Distribui eventos de ruído do mundo.
- `PersistentObject` · É a base de objetos que têm ID e estado salvo.
- `RatScurry` · Controla os ratos, seus trajetos e sons.
- `SaveSystem` · Valida, grava e recupera o checkpoint e o estado dos objetos.
- `SceneCatalog` · Aponta para as cenas e o livro da campanha em uso.
- `SceneTransitionService` · Coordena a troca assíncrona de cenário, os fades e o bloqueio temporário de entrada.
- `SchoolAmbientConfig` · Guarda os sons e intervalos dos eventos ambientes da escola.
- `SchoolAmbientEvents` · Executa eventos ambientes conforme os pontos e regras da escola.
- `SchoolAnchorInspection` · Integra itens âncora ao fluxo de inspeção.
- `SchoolFlashlightDust` · Apresenta partículas de poeira no feixe da lanterna.
- `SchoolHorrorAtmosphere` · Liga iluminação, ambiente e tensões específicas da escola.
- `SchoolMemoryAnchor` · Define um ponto usado para recuperar a sanidade.
- `SchoolObjectiveJournal` · Mantém os objetivos da escola disponíveis para consulta.
- `SchoolPerceptionEvents` · Aplica mudanças de percepção e consequências do blackout à cena.
- `SchoolSanityController` · Calcula a sanidade e determina o momento do blackout.
- `SchoolSanityView` · Aplica a apresentação visual e sonora associada à sanidade.
- `StandaloneSchoolBenchmark` · Executa a medição técnica da escola para diagnóstico.
- `EnemyBrain` · Contém lógica de inimigo de protótipo, separada da campanha psicológica atual.

### Tutoriais e interface

- `TutorialBindings` · Exibe a tecla atual de cada ação, respeitando remapeamentos.
- `TutorialCardCatalog` · Reúne os cartões de tutorial pelo ID.
- `TutorialCardData` · Define texto, controles, imagem ou vídeo de cada cartão.
- `TutorialCardMedia` · Exibe o modelo, vídeo ou imagem estática do tutorial.
- `TutorialCardSystem` · Organiza a fila de cartões e guarda os que o jogador já viu.
- `TutorialCardView` · Desenha a interface de um cartão.
- `AccessibilityPanel` · Mostra os ajustes de acessibilidade.
- `CaptionPresentation` · Aplica o estilo de apresentação das legendas.
- `ChapterCommandTerminal` · Recebe comandos de teste de capítulos, como o atalho da estrada.
- `DisplayAccessibility` · Mantém ajustes de visibilidade e apresentação.
- `GameHUD` · Mostra objetivos, interação, legendas, pausa e demais indicadores.
- `GameplayOptions` · Guarda os ajustes de jogabilidade.
- `PresentationSettings` · Centraliza preferências de redução de efeitos e apresentação.
- `RecordingOverlay` · Mostra os controles e o progresso da reprodução de uma fita.

### Apito

- `WhistleChildAnswers` · Calcula quando e como a resposta das crianças é reproduzida.
- `WhistleConfig` · Guarda alcance, tempo de espera, sons e limites do apito.
- `WhistleDialogueCatalog` · Escolhe as falas associadas a cada gatilho do apito.
- `WhistleHighlightable` · Marca um objeto que pode receber o destaque do apito.
- `WhistleLoreClue` · Reconhece uma pista que explica o apito.
- `WhistlePulseRendererFeature` · Desenha o pulso e os contornos pelo pipeline URP.
- `WhistleResponder` · Liga objetos ou eventos do cenário à resposta de um apito.
- `WhistleSchoolDiscovery` · Coordena a descoberta da mecânica do apito na escola.
- `WhistleSense` · Seleciona as pistas e os objetos elegíveis para o pulso.
- `WhistleSenseCamera` · Fornece os dados da câmera usados na apresentação do pulso.
- `WhistleState` · Guarda as informações do apito usadas no checkpoint.
- `WhistleSystem` · Executa o toque, o tempo de espera, as falas e as respostas.
- `WhistleTrailRoute` · Define os rastros que podem orientar João durante a investigação.

### Menu principal

- `AnalogSignal` · Gera a apresentação do efeito analógico no menu.
- `ChapterSelection` · Exibe e confirma o início do capítulo.
- `HauntedTitle` · Coordena o efeito do título na apresentação do menu.
- `LoadingBenchmark` · Mede o carregamento a partir do menu para diagnóstico.
- `MainMenuController` · Coordena Novo Jogo, Continuar, configurações e a saída.
- `MenuAtmosphere` · Controla o ambiente e eventos de fundo do menu.
- `MenuAudio` · Toca os sons de interface e do ambiente do menu.
- `MenuChoice` · Implementa o comportamento de seleção dos botões do menu.
- `MenuItemFX` · Reage ao mouse e à seleção para animar as opções.
- `SceneFader` · Mostra o fade e os cartões de introdução de Novo Jogo.
- `SettingsManager` · Aplica e salva volume, sensibilidade e opções de tela.
- `WindDoor` · Anima a porta de fundo do menu.
- `Readme` · Guarda os dados do tutorial padrão da Unity.


