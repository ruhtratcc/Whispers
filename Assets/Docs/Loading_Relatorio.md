# Loading — relatório real (31/10/2026)

Concluído em `C:/Users/ruhtra/My project`. Perfil pessoal não utilizado; builds internas de medição, sem ZIP.

## Método e tempos

Development Builds Windows x64, Unity 6000.6.3f1, AMD Radeon RX 580 2048SP, CPU AMD Ryzen 5 5600G with Radeon Graphics . Autoconnect Profiler desligado. Ambas as builds usam o produto isolado `Whispers Loading Benchmark`, 1280×720 HDR, e o mesmo roteiro: três ciclos, 30 transições por build. Foram mantidas esperas de 8 s no escritório e 20 s na estrada, para representar a oportunidade de pré-carregar. Todos os 60 percursos terminaram; saves exclusivos do perfil descartável.

A janela permanece oculta. O teste solicita uma renderização real da câmera do jogo via URP StandardRequest e acompanha endCameraRendering; não usa screenshot como prova de que um frame avançou. O tempo jogável exige inicialização e liberação do controle; no menu, também exige o término da entrada. O teste de referência segura temporariamente a ativação em 90% apenas para marcar esse instante e a libera no frame observado (sobrecusto de até um frame). O jogo normal não recebe essa instrumentação sem a opção explícita de medição. A primeira comparação está preservada em AfterMeasurements-v1: ela mostrou o custo de drenar a escola ao sair imediatamente da estrada e um fade duplicado no menu. Esses casos motivaram o atraso de 3 s no preload da escola e a reutilização do fade já concluído no menu, com áudio previamente atenuado.

| Transição (3 repetições) | Antes média / pior (s) | Depois média / pior (s) |
|---|---:|---:|
| Novo Jogo → escritório | 1.473 / 1.578 | 1.322 / 1.864 |
| Continuar → escritório | 1.416 / 1.422 | 1.050 / 1.051 |
| Escritório → estrada | 0.577 / 1.536 | 0.873 / 0.874 |
| Continuar → estrada | 1.441 / 1.445 | 1.050 / 1.051 |
| Estrada → escola | 2.298 / 5.775 | 0.957 / 0.993 |
| Continuar → escola | 1.871 / 1.968 | 1.121 / 1.137 |
| Escritório → menu | 2.683 / 2.685 | 0.977 / 1.030 |
| Estrada → menu | 2.695 / 2.727 | 0.931 / 0.950 |
| Escola → menu | 2.625 / 2.635 | 0.933 / 0.959 |
| Escola → menu (rodada final) | 2.631 / 2.655 | 0.976 / 0.998 |

Primeira escola: antes 5.775 s; depois 0.993 s. Nas duas repetições seguintes: antes 0.560 s, depois 0.939 s. Os caches do sistema e do driver não foram limpos entre builds; a diferença da primeira entrada não deve ser atribuída inteiramente ao código. A trava de entrada e os fades têm um custo intencional, indicado por esses números, inclusive se uma rota já quente ficar mais lenta.

## Causa demonstrada e decisões

- A troca anterior já era LoadSceneAsync. O problema não era uma chamada síncrona escondida: a carga só começava no fim e não havia um coordenador de pré-carregamento/ativação.
- No escritório, cerca de 1,33 s da referência era fade/áudio antes da emissão da carga. Na volta ao menu, cerca de 2,5–2,6 s era a animação de entrada. Essas esperas foram reduzidas para fades de 0,35 s nas transições; a abertura inicial do menu mantém sua apresentação.
- Na primeira estrada→escola, a referência registrou 3,232 s até 90%, 2,386 s entre 90% e sceneLoaded e cerca de 45 ms até a sessão estar pronta. A maior parte desse intervalo de ativação é nativa/integração de objetos e recursos; não foi atribuída individualmente a física, upload, texturas ou shaders.
- Os Awakes instrumentados do destaque somaram 30,69 ms em 151 chamadas. Captura, carry e gravação de checkpoint ficaram em poucos milissegundos (Save.Write 2,57 ms na primeira escola); não explicam sozinhos os segundos de espera.
- Os capítulos escolares continuam na mesma cena. Não houve divisão aditiva, bake de oclusão, fusão de geometria, mudança de colliders, qualidade, iluminação, FOV ou nitidez do PC. Não foi adicionado Addressables nem outro pacote; nenhuma ShaderVariantCollection foi inventada sem prova de benefício.

## Coordenação, fila e memória

- Preload do escritório ao confirmar Novo Jogo/Continuar; preload da estrada quando os emails obrigatórios e o apito permitem sair; preload da escola 3 s após inicializar a estrada (cancelado naturalmente se sair antes). A ativação mantém o gatilho narrativo existente, inclusive o portão após pegar a lanterna/sair do carro, preservando o silêncio do rádio.
- Uma única operação nativa pode ficar pendente em 90%. Prioridade Low enquanto o mundo roda; High após confirmar no menu/tela de espera e sob a cortina; prioridade anterior restaurada ao terminar. Se o jogador chega antes, aguarda com “Um momento...”, sem porcentagens técnicas.
- A limitação da fila da Unity foi tratada: um destino diferente não inicia outra operação atrás da carga retida. A carga pendente termina escondida, a sessão descartada não inicializa a história nem consome seu handoff; só então o destino solicitado carrega. Isso pode custar mais ao voltar ao menu durante um preload. As medições de estrada→menu exercitam esse caso.
- Uma inicialização rejeitada pode pedir menu enquanto o serviço está ocupado; o pedido é redirecionado com a cortina ativa. Duplicatas não disparam cargas paralelas.
- Handoff entre capítulos e Continuar usam snapshots validados em memória antes da leitura de disco. A gravação das transições é assíncrona/atômica (.tmp/.bak), com payload criado no main thread e somente I/O no worker. Ordem global por caminho e revisão do proprietário impedem um save antigo de sobrescrever uma chegada nova. Save() comum preserva seu contrato síncrono; pode aguardar apenas I/O já em curso, nunca uma continuação que dependa do main thread. A transição não usa essa espera síncrona.
- Falhas fazem uma tentativa por evento, mantêm snapshot pendente e o botão existente de retry; não há tentativas de I/O a cada frame. O encerramento pelo menu permite até 2 s para terminar gravações pendentes.
- Pico medido pelo Unity Profiler: antes 582.9 MiB; depois 581.7 MiB (inclui as amostras do preload). São bytes alocados nativos/gerenciados do Unity, não RAM total do sistema nem VRAM. O uso observado não exigiu cortar assets ou dividir a escola neste PC de 16 GB. O comportamento em máquinas com menos memória permanece NÃO VERIFICADO.

Pré-carregamentos medidos:

| Origem → destino | Até 90% (s) | Pico Unity (MiB) | Maior frame observado durante a carga (ms) |
|---|---:|---:|---:|
| MainMenu → Chapter1Office | 0.726 | 373.9 | 18.45 |
| MainMenu → Chapter1Office | 0.052 | 384.1 | 9.26 |
| Chapter1Office → RoadJourney | 0.914 | 335.9 | 4.78 |
| MainMenu → RoadJourney | 0.083 | 400.2 | 12.50 |
| RoadJourney → SchoolCEEP | 0.565 | 551.9 | 30.18 |
| MainMenu → SchoolCEEP | 0.417 | 574.0 | 64.49 |
| MainMenu → Chapter1Office | 0.050 | 433.0 | 6.85 |
| MainMenu → Chapter1Office | 0.055 | 433.7 | 8.15 |
| Chapter1Office → RoadJourney | 0.065 | 383.9 | 4.32 |
| MainMenu → RoadJourney | 0.079 | 444.8 | 10.90 |
| RoadJourney → SchoolCEEP | 0.433 | 579.2 | 37.10 |
| MainMenu → SchoolCEEP | 0.382 | 575.0 | 73.92 |
| MainMenu → Chapter1Office | 0.060 | 434.0 | 11.96 |
| MainMenu → Chapter1Office | 0.053 | 434.9 | 10.42 |
| Chapter1Office → RoadJourney | 0.060 | 384.8 | 6.56 |
| MainMenu → RoadJourney | 0.086 | 445.6 | 12.27 |
| RoadJourney → SchoolCEEP | 0.428 | 571.9 | 22.92 |
| MainMenu → SchoolCEEP | 0.410 | 575.2 | 41.56 |

Os tempos de frame são amostras de Time.unscaledDeltaTime no processo de teste. Não são estimativa de FPS, tempo GPU isolado ou sensação do controle.

## Conferência de causas comuns

A escola tem 40481 objetos, 36191 ativos e 4859 colliders (41 MeshColliders). Há 4 luzes ativas na cena autorada, nenhum lightmap e nenhuma reflection probe realtime. Nenhum script ausente nas quatro cenas; shaders conferidos sem erros. Não há bake de NavMesh/lightmap nem Resources.LoadAll nos caminhos de gameplay examinados. Não foi feita uma transformação do mapa a partir apenas dessas contagens.

Dependências de importação: 288 texturas (23 sem compressão, 0 com mip streaming), 136 áudios (85 DecompressOnLoad, 8 Streaming, 121 com preload). Isso não demonstra um gargalo por si só. Formatos e aparência foram preservados; custo separado de descompressão, texturas e compilação GPU na primeira renderização é NÃO VERIFICADO. Os intervalos de dados/ativação/inicialização e os marcadores gerenciados estão nos JSONs brutos.

## Portas e regressões

Foram retirados 129 componentes de destaque e seus probes auxiliares. Nenhuma folha, batente ou collider físico de porta foi excluído. Restaram 58 grupos de destaque para os itens; as 12 pilhas continuam reveladas através das paredes. Portas também são rejeitadas pelo componente em runtime para evitar regressão em futuros objetos.

213 testes únicos passaram. Incluem save schema/compatibilidade, escrita síncrona imediata versus escrita assíncrona antiga, falhas de disco e retry no destino, snapshot em memória sem arquivo, retenção em 90%, duplicatas, destino diferente, fallback incompleto/inválido, retorno após startup rejeitado, continuidade sem recarga da escola, ambos os finais com reload e renderização real do x-ray sem portas. Resultados iniciais e repetições foram preservados; o resumo usa o último resultado por nome.

Um assert inicial da Unity veio do helper de teste que mudava GPU Resident Drawer depois do preload: o helper foi corrigido para preservar a configuração. A suíte final passou sem suprimir o assert. A primeira build também revelou PhoneMessagesView usando uma propriedade exclusiva do Editor fora de #if; o getter foi corrigido para permitir compilar Player, sem alterar a mecânica do telefone.

## Arquivos e ajustes

- SceneTransitionService.cs: operação única, preload/ativação/fallback/cortina/áudio/controle. FadeSeconds e InputLockSeconds são os ajustes principais.
- GameSession.cs e NarrativeDirector.cs: entrada segura, handoff, startup descartado e save de transição.
- SaveSystem.cs: gravação assíncrona ordenada e snapshot de Continuar em memória; schema preservado.
- MainMenuController.cs: confirmação/Continuar assíncronos, fades, bloqueio de input e saída segura.
- BeforeDemolitionOfficeFlow.cs / BeforeDemolitionRoadFlow.cs: pontos de preload; ajustar os gatilhos nesses métodos, não criando outra operação de carga.
- WhistleHighlightable.cs / SchoolCEEP.unity: portas excluídas do efeito.
- LoadingMetrics.cs / LoadingBenchmark.cs: instrumentação opt-in e roteiro de medição, inerte fora do perfil autorizado.
- AudioManager.cs, InspectionManager.cs e Awakes instrumentados: scopes de diagnóstico, sem alterar seu trabalho.
- PhoneMessagesView.cs: correção de compilação Player; testes novos e testes do x-ray atualizados.

## Como testar e limites

No Play com save descartável e na build: iniciar jogo, Continuar em cada uma das três cenas, completar emails/apito e sair, dirigir até a escola sem pular a coleta de equipamentos, voltar ao menu durante a estrada, verificar ausência de flash do mapa e input prematuro, repetir os capítulos da escola sem resetar portas/pistas e conferir os dois finais. Soprar perto de uma pilha atrás da parede: ela aparece e as portas não.

NÃO VERIFICADO: campanha humana integral, sensação do cursor/controle, mix auditivo no seu headset, nitidez percebida da cortina em todas as resoluções, tempos em outra máquina, VRAM e separação individual de custo GPU/shaders/texturas/áudio/NavMesh durante ativação. Não foi produzida distribuição ZIP. A apresentação real na janela visível não é equivalente ao frame offscreen do instrumento; os números são do Development Player descrito acima.

Conferência adicional da cena: os blocos autorados de renderers, malhas, luzes e colliders físicos permaneceram iguais ao backup. Foram removidas 126 esferas auxiliares de detecção do destaque; três componentes usavam o próprio objeto e o preservaram. A Unity também deixou de serializar 232 referências stripped a renderers de prefabs após retirar seus últimos vínculos de destaque; isso não excluiu os renderers físicos nem as instâncias de prefab. Prova: scene-preservation.json.
