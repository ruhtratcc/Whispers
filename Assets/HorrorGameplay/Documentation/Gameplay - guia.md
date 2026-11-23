# Whispers of Past — base de gameplay

Implementado e validado no seu projeto Unity 6000.6.3f1, URP 17.6, Input System 1.20 e AI Navigation 2.0.14. Os scripts usam APIs do Unity 6; versões 2022 exigiriam adaptações. O menu existente permanece integrado. A TestScene contém apenas um laboratório de blocos e objetos provisórios; o mapa final fica com você.

## Começar agora

1. Abra `Assets/HorrorGameplay/Scenes/TestScene.unity` e aperte Play. Clique na Game View para capturar o mouse.
2. Explore os seis pisos, a chave sobre a mesa, a porta trancada, as caixas, a gaveta e os dois esconderijos. Há uma escada, uma rampa, teto baixo e um inimigo cápsula.
3. Para testar o fluxo completo, abra `Assets/HorrorMenu/Scenes/MainMenu.unity`. Novo jogo apresenta o capítulo; o botão **INICIAR CAPÍTULO** carrega TestScene. Continuar retoma o último checkpoint. ESC abre a pausa; retornar ao menu restaura tempo, áudio e cursor.
4. Em `Assets/HorrorGameplay/Data/SceneCatalog.asset`, troque **Main Menu Scene** e **Gameplay Scene** pelos nomes exatos das suas cenas, sem `.unity`. Inclua ambas em File > Build Profiles > Scene List. O MainMenuController e o GameSession já referenciam esse catálogo.

## Controles

| Ação | Tecla |
|---|---|
| Andar / olhar | WASD / mouse |
| Correr | Shift segurado |
| Agachar | Ctrl ou C segurado |
| Pular | Espaço, somente com Allow Jump habilitado |
| Interagir / pegar / largar objeto segurado | E |
| Interagir / arremessar objeto segurado | Clique esquerdo |
| Arremessar | Clique direito |
| Largar | G |
| Lanterna | F |
| Inventário e notas | Tab ou I |
| Pausa / fechar leitura | ESC |
| Overlay de gravação | V |
| No esconderijo: prender respiração | Alt segurado |
| No esconderijo: espiar / sair | A/D ou mouse / E |

As ações também têm bindings de gamepad no asset. O HUD atual mostra instruções de teclado; a seleção automática de ícones por dispositivo não faz parte desta base.

## Organização entregue

```text
Assets/HorrorGameplay/
  Scripts/
    Player/       FirstPersonMotor, FirstPersonCamera, FlashlightController
    Interaction/  IInteractable, PlayerInteraction, PhysicsGrabber, PhysicsPickup,
                  Inventory, InteractiveDoor, InteractiveDrawer, CollectibleItem, HidingSpot
    Audio/        AudioManager, SoundSet, FootstepLibrary, SurfaceType,
                  FootstepAudio, ImpactAudio, SpatialAmbience, RandomScareAudio
    Enemy/        EnemyBrain
    UI/           GameHUD, GameplayOptions, RecordingOverlay
    Systems/      GameSession, SceneCatalog, InputReader, SaveSystem, PersistentObject,
                  Checkpoint, EventZone, Jumpscare, FlickerLight,
                  HorrorAtmosphere, DarknessZone, NoiseSystem
  Editor/         GameplayAssets, GameplayUI, GameplayBuilder
  Input/          HorrorInput.inputactions
  Audio/          Gameplay.mixer, FootstepLibrary, SoundSets, Placeholder/*.wav
  Data/           SceneCatalog, GameplayVolume, TestNavMesh
  Prefabs/        GameplayRoot, Player, Door, CabinetDoor, Drawer, PhysicsBox,
                  PhysicsBarrel, Key, Note, Battery, HidingCloset, HidingUnderBed, Enemy
  Scenes/         TestScene.unity
  Tests/          EditMode e PlayMode
  Documentation/  Este guia e imagens
```

Todos os scripts estão completos no projeto. Assemblies de Editor e testes ficam separados do código de runtime. O menu Tools > Whispers > Create Test Scene monta os assets em um projeto preparado; recusa sobrescrever TestScene existente e cenas com mudanças pendentes.

## Player e câmera

Escolhi **CharacterController**: movimento previsível, degraus e limites de inclinação configuráveis, sem tombar ou oscilar como um Rigidbody de personagem. Objetos empurráveis continuam usando Rigidbody; o controlador aplica força limitada pela massa. Não adicione Rigidbody ao Player.

O prefab `GameplayRoot` já contém Player, câmera, input, áudio, HUD, save, sessão e Volume com referências preenchidas. Use uma única instância por cena. O Player separado é útil para editar o personagem, mas sozinho exige os sistemas externos.

Para montar manualmente:

1. Crie Player na layer Player, escala `(1,1,1)`, posicione os pés acima do chão. Adicione CharacterController: radius 0.32, height 1.8, center Y 0.9, skin width 0.035, slope limit 45°, step offset 0.3. Confira os valores finais no prefab entregue.
2. Adicione FirstPersonMotor, Inventory, PlayerInteraction, PhysicsGrabber, FootstepAudio e FlashlightController.
3. Crie uma Camera filha com AudioListener, tag MainCamera e FirstPersonCamera. Ative Post Processing no Universal Additional Camera Data e inclua a layer do Volume no Volume Mask.
4. Conecte FirstPersonMotor.input ao InputReader; view à FirstPersonCamera. Na câmera, conecte motor e playerCamera. PlayerInteraction e PhysicsGrabber recebem a mesma câmera, motor e inventário. FootstepAudio recebe motor e FootstepLibrary.
5. Crie uma filha para a lanterna, com Spot Light, e preencha lamp, motor e input no FlashlightController. O prefab já implementa atraso e balanço da mão.
6. Crie os sistemas e o HUD seguindo as referências do GameplayRoot. Evite manter duas câmeras com AudioListener ou dois EventSystems.

Aceleração/desaceleração produzem inércia. A câmera concentra bob, respiração, FOV, shake e transições; outros scripts usam sua API, sem disputar o Transform. Bob para quando o personagem está parado; respiração é sutil. A stamina bloqueia sprint ao esgotar e exige recuperação parcial. O agachamento consulta o teto antes de levantar; o salto vem desabilitado. Pause, cutscene e morte bloqueiam input de gameplay.

## Input System e layers

Project Settings > Player > Active Input Handling deve incluir **Input System Package (New)**. O projeto já tem o pacote. Abra `Input/HorrorInput.inputactions`: mapas Gameplay, Always e UI. Edite bindings nesse asset, sem criar outro asset paralelo. InputReader instancia uma cópia por cena; o InputSystemUIInputModule usa as ações UI dessa mesma cópia. Always/Pause continua ativo enquanto Gameplay está desabilitado.

Layers criadas em slots livres, preservando as existentes:

| Layer | Uso |
|---|---|
| World | Chão, paredes, teto e geometria fixa |
| Player | CharacterController do jogador |
| PhysicsProps | Caixas e objetos físicos |
| Interactable | Porta, gaveta, itens e esconderijos |
| Enemy | Cápsula/colisor do inimigo |

Mantenha colisões Player × World/PhysicsProps/Interactable/Enemy habilitadas em Physics. O raycast de interação precisa atingir World para paredes o bloquearem e as layers dos objetos para encontrá-los; exclua Player. Máscaras da visão e lanterna devem incluir o mundo. Use colliders reais: um MeshRenderer não cria colisão. Props dinâmicos precisam de Box/Capsule ou MeshCollider **Convex**. Os sistemas de superfície não exigem novas tags: SurfaceType é a opção principal.

## Pisos, impactos e áudio

Em cada piso, adicione SurfaceType e escolha Wood, Concrete, Dirt, Grass, Metal ou Tile. Em FootstepLibrary, configure steps e landing de cada superfície. Também há fallback por PhysicsMaterial, tag correspondente e concreto. Cada SoundSet controla clips, volume e pitch; configure pelo menos dois clips distintos para evitar repetição imediata. Distância percorrida sincroniza passos; correr e agachar alteram cadência, ganho e raio de ruído. Saltos/quedas geram aterrissagem.

Os WAVs entregues são **sons sintéticos provisórios** para testar os sistemas. Substitua por gravações/licenças apropriadas: passos, rangidos, impactos, respiração, ambiência, vozes e música. Para testar a lógica não há assets externos faltando. Para obter naturalidade final, a troca do áudio é necessária.

O `Audio/Gameplay.mixer` contém Music, SFX, Ambience e Voice e parâmetros expostos MusicVolume, SFXVolume, AmbienceVolume, VoiceVolume. AudioManager faz roteamento e converte os sliders lineares em decibéis. Master usa AudioListener.volume. Música calma e tensa fazem crossfade conforme tensão; ambiência e respiração são camadas separadas. AudioManager e GameSession são singletons da cena e são destruídos ao trocar de cena.

Conexão com as opções existentes:

- Master, Music, SFX, Sensitivity e InvertY usam as mesmas chaves `HorrorMenu.*` do seu SettingsManager. Alterações no menu são lidas ao entrar no gameplay.
- A pausa inclui Ambience e Voice. Sem valores próprios salvos, ambos seguem o valor SFX do menu. Quando ajustados individualmente, mantêm seus valores.
- Para adicionar os dois sliders ao menu futuramente, grave `HorrorMenu.Ambience` e `HorrorMenu.Voice` entre 0 e 1 e chame `AudioManager.Instance?.ApplySettings()` quando houver gameplay ativo. O seu menu já funciona com Master/Music/SFX.
- Se trocar o mixer, exponha os quatro parâmetros com os mesmos nomes e preencha os quatro grupos no AudioManager.

SpatialAmbience faz looping 3D e escolhe canal. RandomScareAudio toca SoundSets em intervalos configuráveis e pode aumentar tensão; não precisa emitir ruído de IA. ImpactAudio varia ganho com velocidade/impulso e publica ruído. `NoiseSystem.Emitted` é o evento para outros inimigos escutarem: recebe posição, raio, origem e tipo. Som e percepção usam o mesmo acontecimento, mas são independentes.

## Interação, portas, itens e física

**Props:** use PhysicsBox/PhysicsBarrel ou coloque Rigidbody, collider, PhysicsPickup e ImpactAudio no seu modelo. Configure massa em kg; caixas de teste usam 6 kg. Interpolation e Continuous Dynamic ajudam objetos rápidos. PhysicsGrabber usa forças/torque para seguir a câmera, limita massa e solta quando há obstrução ou distância excessiva. Colisão com o jogador é restaurada ao largar, arremessar, pausar ou desabilitar.

**Porta:** coloque um root parado, um filho Hinge na borda e a folha como filha do Hinge. InteractiveDoor fica no root; preencha hinge, leafCollider e playerMask=Player. Ajuste Open Angle, inclusive negativo para inverter o sentido. A rotação usa o pivot real; escala não uniforme no conjunto deve ser evitada. O colisor da folha participa da colisão e a animação para se atravessaria o jogador. Locked exige um item com ID igual a Required Key. Eventos On Open/Close/Unlock permitem ligar luzes ou disparar história. CabinetDoor usa o mesmo sistema para uma folha menor.

**Gaveta:** InteractiveDrawer aponta ao Transform móvel. Local Travel é deslocamento no espaço local desse Transform; mantenha os trilhos/carcaça fixos. Os prefabs têm colliders e obstáculo de navegação para a carcaça.

**Itens:** CollectibleItem escolhe Key, Note ou Battery. Configure ID estável, título, texto da nota ou segundos de recarga. Key e Note entram no inventário; Battery recarrega a lanterna. Notas abrem leitura com pausa. O objeto coletado é desativado e permanece registrado para restauração do save. A chave de teste é `school_key`.

**Novas interações:** implemente IInteractable (`Prompt`, `CanInteract`, `Interact`) em um componente com collider na layer adequada. PlayerInteraction procura a interface no objeto ou nos pais e atualiza mira/prompt. Não é necessário editar o controlador.

Após duplicar portas/gavetas/colecionáveis, execute **Tools > Whispers > Assign Unique Save IDs** e salve a cena. IDs únicos ligam o objeto ao save; IDs duplicados fazem o sistema recusar o checkpoint em vez de restaurar o objeto errado. Não regenere IDs indiscriminadamente depois de distribuir saves.

## Lanterna, atmosfera e pós-processamento

FlashlightController tem autonomia de 240 segundos no prefab, tecla F, clique, balanço, redução de intensidade e falhas perto do fim. Batteries acrescentam segundos até a capacidade máxima. A pausa congela consumo; durante cutscenes com tempo rodando a lanterna pode continuar consumindo.

TestScene usa ambiente escuro, ausência de skybox e fog **exponencial quadrática**. Não há solução de neblina volumétrica implementada. GameplayVolume configura vignette, film grain, aberração cromática leve, gradação fria/desaturada, bloom suave e ACES. HorrorAtmosphere cria uma instância do perfil para variar tensão/exposição sem alterar o asset original.

Para uma cena nova, copie as configurações de Environment da TestScene: Ambient Source=Color, cor muito escura; Skybox Material vazio; Fog ativado, Exponential Squared, cor escura e densidade sutil (aproximadamente 0.02). A névoa deve preservar a leitura dos objetos próximos. A URP Camera precisa de Post Processing habilitado e máscara compatível com o Volume global. Adaptação ocular aumenta lentamente a exposição limitada quando a lanterna está apagada ou dentro de DarknessZone; nunca cria visibilidade num cenário totalmente sem luz.

FlickerLight deve estar no mesmo GameObject que Light. Ajuste intensidade e falhas no Inspector; TurnOn, TurnOff e BurnOut são invocáveis por UnityEvent. BurnOut encerra a oscilação e dispara seu evento. A tensão opcional em HorrorAtmosphere influencia vignette, aberração, música e respiração. Pode ser desabilitada sem remover os outros sistemas.

RecordingOverlay é opcional, vem desligado e é alternado por V ou pelas opções. Scanlines e REC ficam abaixo das letras do HUD para manter leitura.

## Esconderijos e inimigo

HidingSpot usa três marcadores: Body Pose (posição do personagem escondido), Camera Pose (visão pela fresta) e Exit Pose (pés fora do móvel). Reaproveite HidingCloset/HidingUnderBed e troque a malha. Deixe o collider da interação acessível e Exit Pose fora da carcaça. O CharacterController é desativado enquanto escondido; sair verifica espaço e teto. Entrada/saída têm transição de câmera. Alt prende a respiração por tempo limitado; esgotar gera inspiração audível. Espiar aumenta exposição. Esses prefabs têm NavMeshObstacle para a IA contornar o móvel.

Para o inimigo:

1. Use Enemy.prefab: NavMeshAgent, cápsula com Rigidbody kinematic e EnemyBrain. Troque apenas o visual; mantenha o root, agent e marcador Eyes.
2. Crie Transforms de patrulha na área navegável e arraste para Patrol Points. Player é resolvido pela sessão; pode ser atribuído explicitamente.
3. No root da geometria estática, adicione NavMeshSurface. Use Physics Colliders e layer World. Bake depois de terminar/alterar pisos e paredes.
4. Garanta que o Agent foi colocado sobre o NavMesh e que Radius/Height refletem a cápsula. NavMeshLinks são necessários para conexões que a malha não cobre.
5. Furniture/portas móveis que não entram no bake precisam de NavMeshObstacle, normalmente com Carving. Obstáculos de móveis entregues já estão preenchidos; valide a passagem das suas portas reais. Não marque Player como obstáculo de carving.

Estados: Patrol, Investigate (último ruído), Chase (visão), Search (última posição, tempo limitado). Visão consulta ângulo/distância e raycast; paredes bloqueiam. Crouch reduz alcance de visão. Ruídos ocluídos têm alcance menor. Esconder-se quebra visão normal; um inimigo já ciente e próximo pode perceber respiração. Sem bake o script avisa uma vez e desabilita sua lógica para evitar erros repetidos. É uma base de IA, sem animações/modelo final.

## Sustos, eventos, objetivos e cutscenes

- Jumpscare: adicione BoxCollider Is Trigger, Jumpscare e configure sound, duration, shake, flash e Camera Pose opcional. A entrada do Player ou Fire dispara som, flash, tensão e bloqueio temporário de controle. One Shot evita repetição naquela sessão.
- EventZone: collider Is Trigger + EventZone. Ligue On Enter/Exit a funções públicas pelo Inspector. One Shot limita a entrada. Player com CharacterController e colliders do teste já satisfaz os requisitos de física de trigger.
- Exemplo sem script: EventZone.OnEnter → GameplayRoot/GameHUD.ShowObjective(string); outro listener → GameHUD.Say(string); outro → FlickerLight.TurnOff(). Use campo estático do UnityEvent para o texto.
- DarknessZone sinaliza ambiente escuro para adaptação. Não substitui luzes ou fog.
- FirstPersonCamera.Shake(strength, duration) é a API reutilizável de shake. GameSession.PlayCutscene(focus, duration, strength) coordena câmera e bloqueio. DisableControl/EnableControl podem ser ligados a UnityEvents; EnableControl remove o override da câmera.

Subtítulos e objetivos fazem fade; durante pausa sua duração congela. Os gatilhos comuns e os timers de susto são reiniciados ao recarregar a cena; o save não serializa automaticamente todo UnityEvent já disparado. Para eventos narrativos que precisam sobreviver ao checkpoint, adicione estado próprio derivado de PersistentObject e use CaptureState/RestoreState, seguindo portas/itens como exemplo.

## Checkpoints, morte e HUD

Checkpoint usa collider trigger e um Respawn Pose opcional. SaveSystem escreve JSON em `Application.persistentDataPath/save.json`, substitui de forma atômica e mantém `.bak`. Salva cena, posição/rotação/olhar, agachamento, stamina, lanterna/bateria, tensão, inventário e estados de portas, gavetas e colecionáveis. O checkpoint inicial é criado ao entrar em um novo jogo. Reiniciar recarrega a cena e restaura esses dados; morrer não sobrescreve o checkpoint. Estado do inimigo e posição de objetos físicos soltos não são persistidos.

GameSession usa Playing, Paused, GameOver e Cutscene. ESC pausa Time.timeScale e AudioListener.pause; UI continua respondendo. Áudio de morte permitido durante pausa não é silenciado pelo listener. Morte mostra preto e opções de checkpoint/menu; queda abaixo do limite configurado também mata. Inventário/notas usam pausa sem permitir movimentar a câmera por baixo da janela. Stamina aparece quando incompleta; escondido, o indicador mostra fôlego. Bateria, mira e prompt são discretos.

## Levar para seu mapa

O caminho mais simples preserva iluminação e sistemas:

1. Fora do Play, salve uma cópia de TestScene com o nome do seu capítulo.
2. Mantenha GameplayRoot. Remova o blockout, objetos e eventos de demonstração que não quiser; remova os inimigos de teste antes de criar os seus.
3. Monte seu mapa na layer World, com colliders. Posicione Player sem encostar em paredes/teto. Em GameplayRoot/GameSession, ajuste o objetivo inicial se alterar a mensagem padrão no script; os objetivos seguintes são configuráveis por EventZone.
4. Adicione instâncias dos prefabs de interação e substitua modelos/áudios. Gere IDs únicos depois das duplicações.
5. Limpe/rebake o NavMeshSurface para a geometria nova; não reutilize TestNavMesh sem recalcular.
6. Troque Gameplay Scene em SceneCatalog; inclua sua cena na lista de build. Os botões existentes passam a usar o novo nome.
7. Teste novo jogo, checkpoint, morte, reiniciar e voltar ao menu. Saves antigos de TestScene não são convertidos automaticamente para o novo capítulo.

Alternativa: arraste GameplayRoot para uma cena vazia, configure Environment/fog conforme acima e crie o NavMeshSurface do seu mapa. Não adicione também Player.prefab, pois GameplayRoot já contém um jogador.

## Problemas comuns

| Sintoma | Verificação/correção |
|---|---|
| Input parado | Clique na Game View; confira Active Input Handling e HorrorInput no InputReader; saia da pausa/cutscene. |
| Botão não carrega cena | SceneCatalog com nome exato e ambas as cenas em Build Profiles; catálogo atribuído no MainMenuController e GameSession. |
| Save recusado | IDs vazios/duplicados: Assign Unique Save IDs; cena incompatível ou JSON inválido também são recusados. |
| Player cai ou atravessa parede | Collider ausente/desativado; layer/matriz de colisão errada; escala do Player deve ser 1. |
| Preso em degraus | Step Offset, Skin Width, largura/altura dos degraus e espaços entre colliders; compare a escada de teste. |
| Não consegue sair do esconderijo | Exit Pose dentro de collider ou com teto baixo; mova o marcador para uma área livre. |
| Porta gira pelo centro | Hinge mal posicionado; mova pivot para a borda e coloque a folha como filha. |
| Porta não destranca | ItemId da chave diferente de Required Key; compare `school_key`. |
| Prop não pega | Massa acima do limite, Rigidbody kinematic, collider/layer fora da máscara ou obstrução. |
| IA parada | Bake ausente, Agent fora da malha, Patrol Points inválidos; cheque Console e obstáculos de carving. |
| Pós-processamento ausente | Post Processing desligado na câmera, Volume Mask incompatível, renderer fora da URP. |
| Tela escura demais | Use lanterna/luzes locais, cheque Volume/exposição e Master de áudio separadamente; bloom não ilumina cena. |
| Passos repetidos | SoundSet com só um clip distinto; configure pelo menos dois. |
| Sons inaudíveis | Mixer grupos/parâmetros, sliders salvos, spatial distance e AudioListener.pause; apenas um AudioListener. |
| Imports em outro projeto | Instale URP, Input System, AI Navigation, uGUI/TMP; inclua a fonte TMP existente do menu. |

## Validação

Testes EditMode verificam ações, seleção de áudio, rejeição de save inválido, referências/perfil/mixer e IDs da cena. PlayMode exercita sprint/recuperação/FOV, teto, cantos/escada, salto/aterrissagem, chave/save, checkpoint agachado, pegar/arremessar/soltar, bateria/pausa/morte, esconderijo/respiração e percepção da IA. O teclado virtual passa pelas ações reais do Input System. O fluxo de botões do menu até TestScene, reinício de checkpoint e retorno ao menu também foi exercitado no Editor.

Validação inicial: **5 testes EditMode + 10 testes PlayMode passaram**. Build StandaloneWindows64 Development concluído com **0 erros**. Quatro avisos de dependências: Pipeline sem configuração de runtime (MCP de edição não será servido no executável), dois shaders internos de debug removidos e pragma antigo do shader TMP. O gameplay não usa Pipeline em runtime.

## Ajuste de áudio e visibilidade

O loop de ruído branco Air ambience foi desconectado de Ambient bed e Spatial dripping placeholder. Música, efeitos, passos e respiração continuam ativos. A lanterna passou de intensidade 60 para 90 (+50%), mantendo cone de 52°, alcance de 17 m e o movimento original. A exposição base passou de -0.45 para +0.15 e a luz local do laboratório passou de 1.2 para 3. TestScene recebeu uma luz direcional de preenchimento tênue (Gameplay visibility fill, intensidade 0.35) para facilitar a leitura fora do feixe. A luz de preenchimento pertence à cena de teste; substitua-a pela iluminação do seu mapa quando montar o cenário. Os prefabs Player e GameplayRoot e os defaults do construtor foram atualizados.

A regressão de bateria/recarga, pausa/áudio e morte passou após esse ajuste.

Build Windows atualizado após o ajuste: concluído com 0 erros e 1 aviso de Pipeline sem configuração de runtime.

## Respiração provisória desligada

O clip sintético Heavy breathing foi desconectado de Exhausted breathing no GameplayRoot. O sistema de stamina, tensão e controle de volume da respiração permanece pronto. Para habilitar futuramente, arraste sua gravação para o campo AudioClip de AudioManager > Exhausted breathing dentro do prefab. O AudioManager inicia o loop e ajusta o volume automaticamente.
