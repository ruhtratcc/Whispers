# Whispers of Past — gameplay e narrativa

Aplicado ao projeto Unity 6000.6.3f1 em 01/10/2026. Nenhum mapa novo, voz de diálogo ou modelo Meshy foi criado.

## O que já funciona

- Novo jogo abre o prólogo **O Telefone** no escritório existente. O telefone toca após seis segundos; **E** ou clique atende.
- Primeira ligação com as falas do roteiro; alguns segundos depois, segunda ligação com a legenda infantil “João...”. Sem voz gravada ou gerada.
- Após as chamadas, abrir a gaveta superior da mesa e pegar a pasta de equipamentos e interagir com a saída do escritório. A conclusão apresenta o próximo capítulo e oferece retorno ao menu enquanto o mapa não existe.
- Pausa durante diálogo congela sua duração e retoma mantendo o jogador bloqueado até terminar. Uma ligação interrompida por carregamento recomeça; ligações concluídas não repetem.
- Save v2 guarda narrativa e continua lendo saves v1. Continuar mantém checkpoint; tentar novamente após morte na história recomeça o prólogo.
- HUD de gravação, REC, relógio, marca HD e opção/tecla V removidos do gameplay. O menu conserva seu tratamento visual anterior.
- Filtro de câmera antiga automático nos capítulos após o prólogo: ruído fino, scanlines discretas, separação cromática, suavização e instabilidade de sinal. O escritório fica sem esse filtro. A HUD legível permanece acima da imagem.
- Respiração humana ao correr, com recuperação gradual, e após sustos. Caminhar ou apertar sprint contra uma parede não inicia esforço. Prender a respiração silencia a camada; os ajustes de voz/respiração e volume geral continuam valendo.
- Susto adiciona oscilação amortecida da câmera e respiração. Pausa congela movimento e sinal; pousar não cancela uma reação mais forte.
- Interruptor começa ligado e permite acender/apagar o circuito principal, preservando a iluminação independente da mesa e da rua.

## Estrutura e integração dos próximos mapas

Os scripts anteriores de movimento, interação, lanterna, inventário, física, esconderijos, inimigo NavMesh, áudio e eventos continuam em `Assets/HorrorGameplay/Scripts`. As adições ficam em `Scripts/Narrative`, `Scripts/Audio` e `Scripts/Player`; o shader fica em `Shaders/VintageCamera.shader`.

Dados completos: **Assets/HorrorGameplay/Data/WhispersStory.asset**. Contém sete capítulos, 48 marcos e 14 sequências de legendas. Somente o prólogo tem cena configurada. Os demais destinos ficam vazios até seus mapas existirem.

1. Em cada mapa futuro, colocar o prefab `Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab` no ponto inicial. Configurar `NarrativeDirector.startingChapterId` para o ID correspondente: `road`, `school`, `left-behind`, `memories`, `terrace` ou `last-exit`.
2. Atribuir a cena ao capítulo em WhispersStory e adicioná-la às cenas do Build Profile. O GameSession, SaveSystem, inventário e card de transição já estão conectados no prefab.
3. Usar `StoryInteraction` para inscrições, notas, gravações e ligações futuras. Configurar `sequenceId`/`signalId` definidos no StoryBook; `Activate()` também aceita UnityEvents.
4. Usar `StoryWorldEvent` para observar um marco. `onTriggered` pode ativar aparições, portas, luzes, sons e inimigos. `onRestoreCompleted`/`onRestorePending` restauram somente estados, sem repetir sustos.
5. `GhostAppearance` controla um visual fornecido por você; `PoltergeistEvent` movimenta Rigidbody existentes e dispara UnityEvents. Posicionar os atores e assar o NavMesh será parte de cada mapa.

A transição leva inventário, bateria, tensão e progresso ao próximo mapa; usa o novo spawn e não transporta a posição antiga. IDs persistentes precisam ser únicos.

## Verificação

**21 EditMode + 11 PlayMode aprovados, zero falhas.** Incluem as chamadas completas na cena real, interrupção/carregamento, pausa, equipamentos, eventos sem repetição, destino ainda ausente, reinício após morte, filtro por capítulo, reação e recuperação da câmera e envelopes de respiração.

Compilação das fontes finais de runtime, Editor e testes: zero erros/avisos. Shader compilado nativamente sem erros. Comparação real com filtro ligado/desligado: diferença média RGB 0,001836; 131.944 pixels alterados em 1280×720. O tratamento é deliberadamente discreto. Não é medição de FPS.

Relatórios: `Gameplay-completion-report.json`, `Gameplay-completion-tests-edit.json` e `Gameplay-completion-tests-play.json` em outputs. Créditos das gravações CC0 acompanham os assets e estão em StreamingAssets. Backups anteriores à instalação ficam em `work/GameplayCompletion/Backups`.

## Se algo não funcionar

- Telefone: mirar no receptor e aguardar o toque; depois de uma chamada concluída, ela não fica disponível novamente no mesmo progresso.
- Equipamento indisponível: terminar as duas chamadas e abrir a gaveta superior da mesa. A saída exige o equipamento coletado.
- Próximo capítulo indisponível: configurar seu mapa no StoryBook e no Build Profile; nenhum mapa de teste é carregado como capítulo automaticamente.
- Respiração muito baixa: conferir volume geral e o grupo Voice nas opções. Ela não toca durante caminhada tranquila.
- Inimigo parado: atribuir alvo/patrulha e assar NavMesh; diálogos, pausa e game over interrompem suas ações.
- Eventos repetidos: usar o ID do marco correto e deixar efeitos de susto fora dos eventos de restauração.

Documentação detalhada por componente também instalada em `Assets/HorrorGameplay/Documentation/Story-setup.md` e `Camera-effects.md`.


## Ajuste de interação e áudio — 01/10/2026

O equipamento fica dentro da gaveta superior funcional da mesa, acompanha seu movimento e só pode ser coletado depois das duas chamadas e quando a gaveta abriu pelo menos 65% do curso. O ID persistente foi conservado. O objetivo informa que o equipamento está na gaveta.

O telefone usa dois recortes CC0 de um telefone antigo: levantar o receptor ao atender e recolocá-lo ao terminar cada conversa. Pausar congela a conversa; morte, carregamento ou desativação não produzem um falso som de desligar. Os sons usam SFX e os volumes existentes das opções.

A respiração usa agora "Male Heavy Breathing #2", da Sound Effects Factory, candidato que o usuário ouviu e considerou interessante. Foi mantida uma sequência contínua de 90,925 s, sem repetir respirações individuais, alterar o ritmo/pitch, aplicar EQ ou redução de ruído. Apenas conversão mono 44.100 Hz PCM16, ajuste constante de volume e extremidades de loop em pausas com fades de 25 ms. Os áudios antigos não entram no novo clipe. Gatilhos de corrida/susto, recuperação, pausa e controles de volume continuam iguais. A licença Free permite uso e edição em jogos comerciais, mas proíbe redistribuir o som original/isolado; o MP3 original fica fora de Assets e de pacotes públicos. Créditos/licença em Narrative/CREDITS.txt e StreamingAssets. Validação técnica e importação: Breathing-selected-report.json e Breathing-selected-installed.json. Não há alegação de avaliação auditiva pelo agente.

Cinco testes nativos PlayMode passaram: quatro de telefone e um do prólogo completo, incluindo raycast e coleta com gaveta aberta, checkpoint, interrupção, pausa e reinício. Não foram repetidos os testes antigos da porta ou salinha. Relatórios: Office-interaction-polish-report.json e Office-interaction-polish-tests.json; créditos em Narrative/CREDITS.txt e StreamingAssets.
