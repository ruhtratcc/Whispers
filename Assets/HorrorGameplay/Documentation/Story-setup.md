# Narrativa sem mapas novos — Whispers of Past

Escopo autorizado: narrativa completa que independe de mapa; prólogo do escritório com duas ligações somente legendadas, equipamento e saída. Não há vozes, novos mapas ou modelos finais. Os seis capítulos futuros têm dados e eventos reutilizáveis; sua cena permanece vazia até o mapa existir.

## Instalação pelo passe do Editor

Copiar Scripts/Narrative/*.cs para Assets/HorrorGameplay/Scripts/Narrative, mantendo cada MonoBehaviour no arquivo de mesmo nome. Substituir os três Scripts/Systems correspondentes preservando seus .meta existentes. Nenhum arquivo de teste antigo precisa ser alterado.

Criar StoryBook em Assets/HorrorGameplay/Data/WhispersStory.asset e chamar HorrorGame.StoryContent.Populate(book). SceneCatalog.storyBook = book; preservar MainMenu e Chapter1Office já configurados. StoryContent é o único lugar de população das falas e da ordem do roteiro.

No GameplayRoot da cena Chapter1Office: adicionar NarrativeDirector e PrologueFlow. NarrativeDirector.book = book; game = GameSession existente; inventory = Inventory existente; startingChapterId = prologue. Atribuir GameSession.narrative e SaveSystem.narrative. PrologueFlow.director = director. O GameSession inicializa a narrativa somente depois de restaurar o save.

No telefone existente: adicionar StoryTelephone com collider de seleção; flow = PrologueFlow e ringSound = SoundSet do telefone gravado. PrologueFlow.telephone = este componente; firstCallDelay = 6, secondCallDelay = 4. Sem AudioClip de voz. Toque pausa junto ao AudioListener. E atende apenas quando está tocando. Chamadas concluídas não se repetem ao carregar.

Na bolsa/equipamento existente ou pequeno prop visual: StoryEquipment com director, itemId office_equipment, requiredSignal prologue.second-call e persistentId exclusivo. A interação fica disponível após a segunda chamada. A coleta persiste antes do evento de inventário e desbloqueia a saída no próximo frame.

No ponto de saída já existente: alvo independente StoryExit com collider próprio, director, signalId prologue.exit e requiredSignal prologue.equipment. Não instalar no mesmo GameObject de InteractiveDoor, cujo IInteractable pode consumir a seleção. A porta continua tendo a interação de abrir/fechar existente.

No Canvas existente: criar um card de capítulos sempre ativo, CanvasGroup alpha0/interactable=false/blocksRaycasts=false, fundo escuro, TMP heading/body e botão Voltar ao menu. Adicionar ChapterTransitionView com group, heading, body, returnButton e game; director.transitionView aponta para ele. O botão recebe seu listener em OnEnable e o remove em OnDisable. A tela usa GameState.StoryScreen: cursor livre, player bloqueado; ESC retorna ao menu. Não colocar esse card dentro de um painel que o GameHUD desativa em RefreshState.

NarrativeDirector.vintageCamera pode ser atribuído; caso vazio, é encontrado na câmera do jogador. ConfigureForChapter recebe o índice real: prólogo0 sem filtro, futuros1+ com filtro. O card de mapa indisponível mantém o escritório limpo.

## Ordem do prólogo e save

Explorar → telefone toca → E atende → diálogo inicial completo → intervalo → segundo toque → E atende → legenda João... → pegar equipamentos → E sair. A primeira chamada inclui apenas a indicação [A voz informa o endereço da escola.]; nenhum endereço foi inventado.

GameSession.Pause aceita Playing e Cutscene. Resume restaura o estado anterior; diálogo não devolve movimento no meio da ligação. Fila de legendas, temporizadores e coleta de item aguardam sair de uma pausa. Signal não avança a história em Paused, GameOver ou StoryScreen.

SaveData v2 inclui NarrativeProgress, mas SaveSystem.TryRead continua aceitando v1. Carregar checkpoint restaura inventário, objetos, câmera/player e narrativa antes de iniciar o fluxo. Uma fala interrompida recomeça do início; uma fala concluída não repete. Tentar novamente após morte na história retorna ao prólogo, conforme Fase1 do roteiro; Continuar no menu continua lendo o checkpoint. TestScene sem narrativa mantém o reinício de checkpoint existente.

Concluir capítulo salva chapterId atual e pendingChapterId seguinte. Se a próxima cena ainda não existe, a apresentação mostra o nome do capítulo e Esta parte da história ainda não está disponível., com Voltar ao menu e ESC; nenhum LoadScene inválido é tentado. Quando um mapa futuro existir, configure seu sceneName, adicione-o à lista de cenas e coloque NarrativeDirector/SaveSystem no spawn novo. LoadChapterCarry leva inventário, bateria, tensão e progresso para a cena; não leva poses/objetos do mapa anterior. O primeiro save no novo mapa atualiza o checkpoint.

## Eventos reutilizáveis para os mapas futuros

NarrativeDirector.Signal(id) aceita apenas o próximo marco e os itens exigidos. PlaySequence(id) reproduz captions e conclui o marco associado, somente uma vez. HasSignal/HasSequence servem para condições. CaptureProgress/RestoreProgress fazem cópia profunda do estado. onSignal e onChapterCompleted são UnityEvent<string>; Initialized/SignalAccepted/SequenceStarted/SequenceCompleted são eventos C#.

StoryInteraction: sequenceId para inscrição, gravador ou ligação futura; signalId para marco sem diálogo; requiredSignal para condição e prompt para E. Activate() funciona por UnityEvent. Para desenho/nota que exige um item, reutilizar CollectibleItem: seu Inventory.Add vem antes de onCollected; ligar onCollected a StoryInteraction.Activate. A coleta não pula a leitura da inscrição.

StoryWorldEvent observa um signalId e chama onTriggered no momento em que ele ocorre. Ligar esse evento a portas, interruptores/luzes, EnemyBrain encounters, GhostAppearance.Show, PoltergeistEvent.Fire ou jumpscare. onRestoreCompleted/onRestorePending servem para restaurar silenciosamente o estado visual ao carregar; não ligar susto/voz nesses dois eventos. Listeners são removidos em OnDisable.

GhostAppearance controla um visual filho já fornecido pelo autor do mapa. appearanceAnchor define pose; player e approachDistance controlam a aproximação; approachedSignal é opcional. Show/Hide/RestoreHidden não geram modelos. PoltergeistEvent.Fire aplica impulse nos Rigidbody existentes e chama onFired, ao qual podem ser ligados sons, portas e luzes. Nenhum destes componentes cria ambiente ou inimigo final.

## Verificação

14 testes EditMode: conteúdo, falas, 7 capítulos, ordem estrita, itens, duplicação, cópia, transição, final e leitura de save v1/v2. 7 testes PlayMode isolados: duas chamadas, pausa durante chamada, chamada enfileirada durante pausa, equipamento coletado em pausa, restauração sem repetição, próximo mapa ausente e restauração silenciosa de eventos. Nenhum teste de porta, vestíbulo ou escritório antigo é repetido.

Compilação offline usa Runtime.verify.rsp, EditMode.Tests.verify.rsp e PlayMode.Tests.verify.rsp no diretório do projeto; os testes precisam do TestRunner nativo para execução (a compilação não constitui execução).
