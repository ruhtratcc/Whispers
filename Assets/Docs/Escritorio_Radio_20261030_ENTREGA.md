# Escritório e rádio — entrega de 30/10/2026

Cadeira do PC substituída por GreenChair_01 da Poly Haven já existente no projeto. A anterior foi desativada; ponto de sentar, interação do PC e colisão antiga foram preservados. Captura real revisada confirma ausência do clarão sob a cadeira. O teste de isolamento localizou o brilho no renderer Black Reflective Plastic da cadeira antiga; especular reduzido não resolveu. Não foi uma nova Light sob a cadeira e a lanterna já estava indisponível no escritório.

Porta do escritório: alvo real da folha agora executa a saída da história, sem animação de abrir. O componente antigo de porta fica desabilitado/fechado, inclusive ao recuperar checkpoint antigo aberto. Pré-requisitos de emails 1/6/7 e apito permanecem.

Lanterna do carro: restauração de visibilidade preserva renderers/colliders desativados; dois modelos aposentados não ressurgem. Pickup aponta para o modelo compacto atual, com corpo e lente. Lanterna de mão continua escondida antes da coleta. Coletar continua sem sair do carro automaticamente.

Celular reposicionado no painel pela superfície real da malha, inclinação acompanhando o painel e pequena folga de 6mm no ponto de apoio. Modelo, vibração, coleta, chamadas e mensagens preservados. O teste de movimento sobrenatural agora desvia o olhar explicitamente: o celular está visível por padrão no novo painel e só muda de lugar fora da visão.

Rádio: noticiário existente → três estações musicais → desligamento → 2s de silêncio → instrumental da cantiga. Botão de estação troca música; botão de energia antecipa desligamento. João comenta após ouvir cada estação, com quatro legendas e slots de gravação humana preparados. Estações também avançam automaticamente para não prender a progressão. Pausa e retorno ao checkpoint não repetem as reações já passadas. Flag aditiva radioSwitchedOff mantém estado de desligamento; enum/versões anteriores mantidos.

Músicas: obtidos previews públicos sem DRM de 4 Cavala, Definição de Pecado e Turbulência, do KENTO MC. O jogo usa recortes de 18s de cada; NÃO são downloads integrais. Autorização relatada pelo usuário, sem classificá-las como CC0. Fontes/hashes em music-provenance.json e créditos/LICENCAS atualizados. Há MP3 do recorte de 4 Cavala nesta entrega; Unity usa WAV importado/comprimido pelo AudioImporter.

## Validação real

54 testes únicos passaram: escritório 7, estrada 13, save/compatibilidade 33, restauração de visuais/colliders 1. Resultados brutos e resumo acompanham a entrega; repetições não entram na contagem. Compilação final sem erros. Testes usam diretórios temporários antes do Awake e restauram preferências. Save pessoal não acessado. Story asset e OfficeInteraction.cs têm SHA256 idêntico ao backup anterior.

Capturas reais revisadas: office-chair-fixed.png, road-dashboard-fixed.png, road-torch-fixed.png. Editor devolvido à cena original parado após os testes; final-editor-state.json registra o estado. Nenhum ZIP gerado, nenhuma alteração na escola/menu/FOV49 e nenhuma entidade agressiva acrescentada.

## Arquivos principais

- Assets/ChapterOneOffice/Scenes/Chapter1Office.unity — cadeira/porta e visibilidade inicial do equipamento.
- Assets/RoadChapter/Scenes/RoadJourney.unity — modelos antigos ocultos, pickup/telefone e rádio.
- BeforeDemolitionOfficeFlow.cs — manter porta fechada ao restaurar.
- RoadJourneyInteraction.cs — preservar estados de visuais/colliders na restauração.
- BeforeDemolitionRoadFlow.cs / BeforeDemolitionRoadRadio.cs / BeforeDemolitionRoadState.cs — estações, legendas, desligamento e persistência.
- Tests/PlayMode/BeforeDemolitionOfficeSceneTests.cs e BeforeDemolitionRoadSceneTests.cs — regressões em cenas reais e capturas.
- Tests/EditMode/RoadPickupVisibilityTests.cs — regressão do pickup.
- Assets/Audio/Music/KentoRadio/ — três trechos musicais e instrumental provisório.
- Assets/ThirdParty/Sketchfab_Round2/Materials/stylish-office-chair-game-ready-asset-{0,1,2}.mat — materiais da cadeira aposentada ficaram foscos durante o diagnóstico; modelo sem uso ativo.
- Assets/CREDITS.md / Assets/ThirdParty/LICENCAS.txt — fontes, adaptações e autorização relatada.

NÃO VERIFICADO: mix por ouvido em hardware real, qualidade das emendas na experiência completa, build executável e dublagem nova ainda ausente. Cantiga cantada e quatro novas falas do João ficam pendentes de gravação; não houve TTS.
