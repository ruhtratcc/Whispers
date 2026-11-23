# Entrega — Dicas de item

Cinco cartões completos e integrados: lanterna, equipamentos, apito, grampo e celular. Fila por prioridade/eventos; espera contexto seguro, falas e fechamento de inspeção. Ajuda na pausa, três modos, confirmação para desligar, glifos atuais/rebinding, mídia configurável, pausa/duck e estado salvo com migração de saves antigos. Não exige downloads nem pacote novo.

## Arquivos criados/alterados

Os caminhos Scripts, Tests e Data abaixo são relativos a Assets/HorrorGameplay/.

Scripts/Tutorial/TutorialCardData.cs — dados editáveis, estado versionado e preferências isoladas em testes.
Scripts/Tutorial/TutorialCardCatalog.cs — catálogo dos cinco cartões.
Scripts/Tutorial/TutorialBindings.cs — teclas atuais, gestos dedicados e glifos de mouse.
Scripts/Tutorial/TutorialCardSystem.cs — eventos, fila, contexto seguro, pausa, Ajuda e save.
Scripts/Tutorial/TutorialCardMedia.cs — câmera de inspeção reutilizada, vídeo com Prepare/fallback/liberação.
Scripts/Tutorial/TutorialCardView.cs — cartão responsivo, glifos, confirmação, menus e navegação.
Scripts/Systems/GameSession.cs — estado modal Tutorial e referência do serviço.
Scripts/Systems/SaveSystem.cs — campo aditivo, captura/restauração/migração e validação.
Scripts/Systems/GameClock.cs — relógio não avança no cartão.
Scripts/Audio/AudioManager.cs — duck suave com tempo não escalado.
Scripts/Narrative/NarrativeDirector.cs — impede sinais/sequências durante o modal.
Scripts/Equipment/EquipmentController.cs — notifica a aquisição real da lanterna.
Scripts/Whistle/WhistleSystem.cs — notifica lore e aguarda o cartão antes do sopro.
Tests/EditMode/TutorialCardTests.cs — dez casos de dados, fila, glifos e compatibilidade.
Tests/PlayMode/TutorialCardPlayTests.cs — modos, bloqueios, rebinding, confirmação, pausa e H.264 real.
Tests/PlayMode/TutorialCardVisualTests.cs — render em quatro formatos, custo e checkpoint nativo.
Tests/PlayMode/BeforeDemolitionMirrorTests.cs — fixture compartilhada desliga prompts alheios em perfis descartáveis.
Tests/PlayMode/BeforeDemolitionOfficeSceneTests.cs — isolamento dos prompts em testes anteriores.
Tests/PlayMode/BeforeDemolitionRoadSceneTests.cs — mesmo isolamento.
Tests/PlayMode/BeforeDemolitionSchoolSceneTests.cs — mesmo isolamento; novos testes optam por Completas.
Data/TutorialCards/ — cinco cartões, catálogo, três sons, cinco imagens e prefab visual da lanterna.
Tests/Fixtures/Tutorial/codec_probe.mp4 — clipe técnico de teste H.264, sem áudio.
Assets/ChapterOneOffice/Scenes/Chapter1Office.unity — referência do serviço e catálogo.
Assets/RoadChapter/Scenes/RoadJourney.unity — mesma integração.
Assets/SchoolCEEP/Scenes/SchoolCEEP.unity — mesma integração, portas/mapa preservados.
Assets/Docs/Demos_Para_Gravar.md — roteiros de captura e nomes finais.
Assets/Docs/TutorialCards_ComoTestar.md — três passos, gatilhos, ajustes e checklist.
Assets/Docs/TutorialCards_Entrega_20261101.md — entrega e provas.
Assets/Videos/Tutorial/LEIA-ME.txt — pasta e parâmetros para os vídeos do usuário.
Assets/CREDITS.md / Assets/ThirdParty/LICENCAS.txt — reutilização local registrada.

## Configuração e uso

Veja Assets/Docs/TutorialCards_ComoTestar.md: adicionar um cartão em três passos, prioridade, atraso, bloqueio, layout, gatilhos e checklist. Dados em Assets/HorrorGameplay/Data/TutorialCards/.

A aquisição da lanterna acontece no carro; escritório é excluído. Apito usa whistle.lore_known no fim da anotação da biblioteca, antes do sopro. Equipamentos usam a segunda aquisição; grampo usa o ID lockpick; celular usa a primeira mensagem depois da aquisição. Se o momento estiver bloqueado, a fila é preservada. Ajuda lista somente descobertas, incluindo dicas suprimidas com o modo Desligadas. Não foram criados cartões extras nem alterados IDs ou texto de história.

## Vídeos e imagens

H.264/MP4 escolhido para Windows. Player com loop, Skip On Drop, Prepare/primeiro frame, UnscaledGameTime, fallback e liberação de RT/VideoPlayer ao fechar. O usuário ainda precisa gravar as demos: Demos_Para_Gravar.md detalha apito e grampo, posições, duração, exportação e ligação do VideoClip. Assets/Videos/Tutorial/ é a pasta final. Recorder não estava instalado e não foi adicionado.

Enquanto isso: lanterna real em 3D, HUD/celular em diagramas originais, minigame em imagem real e apito com “DEMO APITO — GRAVAR”. O modelo foi extraído respeitando objetos ocultos: somente a lanterna compacta ativa, sem o modelo antigo. Imagens mantêm proporção original (NPOT sem redimensionamento).

## Testes reais

Compilação sem erros. 398 testes únicos aprovados, zero falhas finais: 348 EditMode (10 novos), 13 novos PlayMode (inclui escola real, quatro formatos, vídeo técnico, confirmação e glifo mudando com cartão aberto), 15 de equipamento, 18 de apito, 3 de grampo e uma progressão principal física do capítulo 3.

O teste nativo coleta o grampo por E, espera a fala de João, verifica congelamento da bateria/eventos e mensagem pendente, fecha e salva/carrega sem repetir o cartão. O teste de vídeo usa um clipe técnico original de 2 s, 320×180, 24 fps, sem áudio; comprovou frames avançando em Time.timeScale=0 e retorno dos recursos pertencentes ao sistema ao baseline. O teste do modelo confirmou pixels reais da lanterna e liberação da RT. Caches internos da URP/decodificador não são contados como RTs pertencentes ao tutorial.

Capturas 1920×1080, 1440×900, 1024×768 e 2560×1080 foram geradas no renderer real da Unity, sem overflow de texto/controles. São provas isoladas da UI, não uma campanha completa filmada. Prévia da lanterna também foi renderizada e revisada. tests.json contém os resultados brutos.

Custo medido via ProfilerRecorder: média 182840 bytes no frame de abertura, baseline 8932 bytes; fechamento 8892 bytes. Inclui UI/fixture/fundo do frame, não custo isolado exato por chamada. GC.GetAllocatedBytesForCurrentThread não foi usado para alegar zero alocação. Preparo do clipe técnico no último teste: 82.5 ms; não é previsão para o vídeo final 960×540/720p.

## Decisões, limites e pendências

- Ajuda foi integrada à pausa; menu principal mantido. Nenhuma leitura extra do save pessoal para popular menu fora da sessão.
- Fonte/estilo reaproveitados; inspeção/PC/celular existentes não foram refeitos. Só os novos cartões usam o novo modal. FOV 49 e o mapa permanecem.
- Ações expostas acompanham remapeamento ao vivo; os gestos fixos do minigame e a tecla do celular seguem o código/Inspector atual. Não foi inventado menu de remapeamento.
- A UI mostra fallback enquanto prepara o primeiro frame de vídeo, evitando imagem de RT ainda vazia. Só texto não cria mídia. Dicas desligadas não impedem Ajuda.
- Os testes anteriores usam modo Desligadas em diretórios temporários para continuar testando suas próprias mecânicas; os testes novos habilitam Completas explicitamente.
- Foi necessário reiniciar a Unity após travamento do Editor. O guard confirmou cena salva antes do trabalho; backups locais existem. A primeira tentativa de reabrir sem o canal do Hub falhou na licença; a reabertura com o canal existente recuperou o Editor, sem instalar/atualizar nada.
- NÃO VERIFICADO: sensação com teclado/mouse humano, mix em fones, vídeo final gravado pelo usuário, vídeo com trilha opcional, campanha inteira manual e standalone. Codec/primeiro frame/loop com jogo parado foram verificados apenas com o clipe técnico.
- Nenhuma narração/TTS, áudio sintetizado, história nova ou ZIP. Save pessoal preservado; restauração final do Editor registrada em final-editor-state.json.

Teste nativo adicional: leitura da biblioteca enfileira o cartão do apito, o sopro aguarda e a hesitação original continua intacta após fechar.

Verificação final: Ver de novo também re-prepara o vídeo após fallback controlado; teste aprovado em video-retry-tests.json. Unity restaurada na SchoolCEEP, parada, cena limpa, sem override de save de teste nem escritas pendentes, execução em segundo plano false; um serviço e cinco cartões ligados.
