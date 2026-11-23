# ElevenLabs SFX — entrega de 04/11/2026

**Ferramenta concluída; geração parcial por limite de saldo.** Lista inteira validada:162 sons,314 variantes,1334,8s solicitados (22min14,8s). Geração processada:248 variantes de130 sons completos;893,817s (14min53,817s) de áudio final. Restam66 variantes de32 sons. Nenhum áudio falhou no estado final. Não é a conclusão integral da lista.

**Cobrança informada pelo cabeçalho da API neste lote:9801 créditos.** Inclui as duas gerações rejeitadas por sinal baixo e suas novas tentativas (5 créditos cada nova tentativa). Estimativa conservadora inicial:87640 créditos; teto local30000, parada27000. Base da conta verificada10000; saldo efetivo final199. A consulta final ainda informava271 por atraso do contador, por isso os recibos/reservas locais também limitam o saldo. A menor próxima reserva é200 créditos, e --budget-fit ainda exige10% de margem. Não houve compra, upgrade, troca ou criação de contas.

**Licença: plano Free, uso não comercial e atribuição obrigatória; estes sons NÃO são CC0.** Uma entrada para este conjunto foi adicionada a Assets/ThirdParty/LICENCAS.txt. A proveniência anterior dos quatro apitos foi preservada. Rever a licença antes de distribuição comercial.

## Lista por seção

| Seção | Sons | Variantes previstas | Geradas | Segundos previstos |
|---|---:|---:|---:|---:|
| Ambiências | 18 | 18 | 18 | 520.0 |
| Jogador | 17 | 43 | 43 | 114.9 |
| Portas, móveis e objetos | 41 | 121 | 121 | 222.8 |
| Lockpick | 6 | 6 | 6 | 9.1 |
| Apito e respostas | 10 | 10 | 10 | 17.0 |
| Ratos e sons do prédio | 11 | 33 | 15 | 93.0 |
| Terror | 14 | 38 | 5 | 116.5 |
| Interfone, rádio e carro | 11 | 11 | 9 | 40.5 |
| PC retrô | 14 | 14 | 11 | 72.5 |
| Celular e telefone | 6 | 6 | 4 | 15.5 |
| Finais | 8 | 8 | 0 | 110.0 |
| Interface | 6 | 6 | 6 | 3.0 |

## Arquivos

- Tools/Audio/sfx_pipeline.py — parser, orçamento, geração offline, retomada por hash/bruto, ffmpeg e chamada automática do importador.
- Tools/Audio/sfx_config.json — categorias, prioridades, volumes/pitch, mono/estéreo e alvos de loudness.
- Tools/Audio/test_sfx_pipeline.py —14 testes offline, inclusive saldo atrasado, reservas/retries, dry-run, chave redigida e retomada.
- Tools/Audio/requirements.txt — biblioteca padrão Python3.10+; ffmpeg externo reutilizado.
- Assets/HorrorGameplay/Scripts/Audio/SfxCatalog.cs — catálogo integrado ao SoundSet.Next e ao pool/mixer/rolloff do AudioManager existente.
- Assets/HorrorGameplay/Editor/SfxBatchImporter.cs — menu Tools > Áudio > Importar SFX, importadores nativos, backups, slots explícitos e relatório de nulos.
- Assets/HorrorGameplay/Tests/EditMode/SfxCatalogTests.cs —3 testes do catálogo e seleção sem repetição.
- Assets/HorrorGameplay/Tests/EditMode/SfxBatchImporterTests.cs —11 testes nativos de caminhos, mono/streaming/latência e dos8 grupos de mixer existentes.
- Assets/HorrorGameplay/Data/SfxBatch/SfxCatalog.asset e Sets/ —130 entradas e seus SoundSets com248 variantes.
- Assets/Docs/Audio/sfx_lista.txt — lista enviada intacta; aceita cabeçalhos com/sem ##.
- Assets/Docs/Audio/sfx_manifest.json — prompts/parâmetros exatos, recibos, estados, hashes, QA e pendências.
- Assets/Docs/Audio/sfx_bindings.json — ligações explícitas com evidência; sem busca aproximada e sem substituir falas/música.
- Assets/Docs/Audio/ElevenLabs_API_Notas.md — documentação primária datada, preço divergente e licenciamento.
- Assets/Docs/Audio/COMO_GERAR.md — ambiente, ffmpeg, dry-run/test/run, retomada e regeneração.
- Assets/Docs/Audio/Sons_Sem_Slot.md e sfx_import_report.json — ligações, pendências e317 referências nulas pré-existentes no escopo auditado.
- Assets/Docs/Audio/sfx_processing_proof.json, sfx_native_proof.json e sfx_preservation_proof.json — provas finais de áudio/importação/preservação.
- Assets/Audio/Raw/Editor/ — brutos fora do Player pela pasta Editor e ausência de referências runtime; .gitignore sozinho não exclui arquivos do build.
- Assets/Audio/SFX/<categoria>/ e Assets/Audio/Ambience/ — processados WAV48k/16bit e OGGq6.
- Assets/_Substituidos/SfxBatch20261104/ — cópias anteriores de clipes/metas/slots/catálogo e fontes rejeitadas.
- .gitignore e Assets/ThirdParty/LICENCAS.txt — cache/credenciais/brutos locais excluídos do Git e licença do conjunto registrada.

## O que foi ligado

62 ligações de slots em assets existentes, incluindo os passos reais de madeira/concreto/cerâmica/carpete, aterrissagem em cerâmica, portas genéricas, gavetas, pilhas/chaves/papel, lanterna, grampo, gravador, PC, celular, apito e quatro respostas infantis.130 entradas/248 variantes no catálogo. Oito grupos encontrados nos mixers existentes:Master,Music,Ambience,SFX,Voice,UI,PC,Phone; os três últimos ficam em assets separados. Não foram criados novos gatilhos de gameplay.

Os nomes sem slot ficam disponíveis no catálogo. Sons de corrida/escadas/ambientes específicos não recebem novos gatilhos por aproximação; a lógica atual de passos e ambiências continua. Os passos antigos de superfícies não contempladas permanecem. Todos os destinos e sobras estão em Sons_Sem_Slot.md. Slots de AudioClip recebem a primeira variante; SoundSets recebem a lista completa.

## Verificações reais

- 14/14 testes Python +3/3 SfxCatalogTests +11/11 SfxBatchImporterTests =28 testes únicos aprovados.
- Três testes reais de API aprovados:passo curto +ambiente do escritório30s +interior do carro30s;610 créditos cobrados. Primeiro passo foi reprocessado do mesmo bruto após correção do recorte, sem outra chamada paga.
- Todos os248 processados foram novamente decodificados fora da Unity, com hashes, duração, amostras finitas, RMS/pico e24 loops sem emenda suspeita. Loudness medido dentro de1LU dos alvos configurados. Nenhum prompt modificado.
- Conferência nativa:130 entradas,248 AudioClips decodificados,48kHz, mono nos3D, streaming nos loops longos, baixa latência UI/PC, mixers resolvidos e zero referências aos brutos no catálogo.
- 317 campos nulos pré-existentes auditados;0 novos nulos. Dublagens e placeholders narrativos não foram preenchidos com sons aleatórios.
- 428 GUIDs/metas de backup comparados sem divergência;55 SoundSets existentes conservaram todos os campos exceto seus arrays de clipes.
- Compilação final sem erros. Console atual:0 erros,7 avisos de código anterior (APIs obsoletas/analisadores em ferramentas do menu/mapa e testes de modelos). O buffer histórico conserva erros antigos, inclusive um nome de enum corrigido nesta execução; não foi apagado para esconder histórico.
- Editor final parado, cena Untitled limpa, sem override de save de testes. Save pessoal não acessado. Menu, prólogo, mapa, FOV49, IDs/flags e schema de save não foram alterados. Nenhum ZIP/build criado.

## Como testar e retomar

Na Unity, Tools > Áudio > Importar SFX (Editor limpo e fora de Play); a última importação automática retornou sucesso. Em um perfil de teste descartável, ouvir passos por piso, portas, gavetas, coleta de pilhas/chaves, PC/celular e o apito na escola após a descoberta. Conferir volume com fones e alto-falantes. Não usar o save pessoal para QA.

Para conferir a fila sem cobrar:python Tools/Audio/sfx_pipeline.py --dry-run. Para geração futura:--run --budget-fit, SOMENTE depois de verificar uma base financeira compatível com saldo novo; não apagar o ledger. --run --only nome (ou nome_02) força nova cobrança desse som/variante. --section "PC retrô" força a seção. Instruções completas em COMO_GERAR.md.

## Decisões e problemas corrigidos

- As sete durações de0,2/0,4s recebem0,5s na API e recorte local identificado, sem alterar prompt. Sons de terror de até5s são considerados curtos; sting_mirror/glass_shatter_mirror têm2 variantes, outros curtos3; loops1.
- Longos >8s, ambiências, respiração/batimentos e loops usam OGG; os curtos usam WAV48k/16bit. Alvos:-30LUFS ambiente,-32 drones,-28 respiração/batimento; curtos pico-3dBFS/fades7ms. Emendas têm medição de salto/correlação/energia e crossfade circular50ms quando necessário.
- O primeiro recorte exigia som sustentado e apagava um passo curto; foi substituído por medição das bordas, preservando transientes e10ms. Bruto reutilizado sem pagar de novo.
- Duas gerações vieram abaixo do piso RMS:uma variante da lanterna e o envio de mensagem. Foram rejeitadas e geradas novamente uma vez, aprovadas. Brutos antigos e recibos permanecem. Não houve repetição infinita.
- Os mixers dedicados UI/PC/Phone foram localizados em seus assets próprios; não foi criada outra estrutura de mixer.
- A primeira importação excedeu o timeout curto do cliente, mas concluiu no Editor. Timeout ajustado e resultado final/prova nativa confirmados. Reimportação evita mudar configurações já corretas.
- Falta de saldo interrompe somente a geração.66 variantes continuam pendentes; os sons antigos dos slots correspondentes foram preservados. Não houve troca automática de contas.

Loops que receberam crossfade circular: lockpick_scrape. Todos passaram na verificação final da emenda; a escuta segue pendente.

## NÃO VERIFICADO

Escuta humana, adequação de timbre/presença/volume no ambiente, ausência de detalhes vocais indesejados no conteúdo gerado, experiência com fones/alto-falantes, campanha completa e Player standalone. Não declarar qualidade artística final com base em RMS ou teste automatizado. Licença comercial não adquirida.

## Pendências de geração

- scratch_wall: scratch_wall_01, scratch_wall_02, scratch_wall_03
- building_creak: building_creak_01, building_creak_02, building_creak_03
- water_drip: water_drip_01, water_drip_02, water_drip_03
- school_bell_far: school_bell_far_01, school_bell_far_02, school_bell_far_03
- branch_snap_far: branch_snap_far_01, branch_snap_far_02, branch_snap_far_03
- tree_creak: tree_creak_01, tree_creak_02, tree_creak_03
- wall_crack: wall_crack_01, wall_crack_02, wall_crack_03
- bricks_fall: bricks_fall_01, bricks_fall_02, bricks_fall_03
- light_flicker: light_flicker_01, light_flicker_02, light_flicker_03
- bulb_die: bulb_die_01, bulb_die_02, bulb_die_03
- power_down: power_down_01, power_down_02, power_down_03
- cold_gust: cold_gust_01, cold_gust_02, cold_gust_03
- door_creak_alone: door_creak_alone_01, door_creak_alone_02, door_creak_alone_03
- small_steps_far: small_steps_far_01, small_steps_far_02, small_steps_far_03
- hand_on_glass: hand_on_glass_01, hand_on_glass_02, hand_on_glass_03
- glass_resonance: glass_resonance_01, glass_resonance_02, glass_resonance_03
- room_warm_swell: room_warm_swell_01, room_warm_swell_02, room_warm_swell_03
- radio_static: radio_static
- car_turn_signal: car_turn_signal
- pc_fan: pc_fan
- crt_hum: crt_hum
- crt_static: crt_static
- phone_vibrate_strong: phone_vibrate_strong
- office_phone_ring: office_phone_ring
- siren_police_pass: siren_police_pass
- siren_ambulance_pass: siren_ambulance_pass
- siren_far: siren_far
- tv_room_tone: tv_room_tone
- bad_end_wind: bad_end_wind
- door_far_close: door_far_close
- demolition_far: demolition_far
- chair_slide_empty_room: chair_slide_empty_room
