# Estrada, postes e rádio assombrado — 30/10/2026

Aplicado no projeto existente.

## Cenário e motor

A auditoria encontrou pista/carro sem rotação ou curvas de trajetória, mas faixa central deslocada da pista e sem enquadramento de faixa coerente. O asfalto e a faixa central agora compartilham X=-1,3; carroX=0 fica no centro da faixa direita,1,3m à direita da divisão. Acostamentos, duas novas calçadas e linhas de limite seguem paralelos. Sem mudar portão, escola, posição final384Z ou FOV49. A prova é geométrica e em Play; a perspectiva natural da câmera continua.

25 postes prontos Street Lamp01/Josh Dean/Poly Haven CC0, glTF1K,2,17MB baixados sem login. Conversão pelo importador nativo já existente; sem dependência runtime. LOD0=30610tris,LOD1=17392. Metal desgastado, vidro translúcido e lâmpada fraca. Luzes quentes Point55/range19/sem sombras pesadas, alternadas a cada75m. Peças antigas de postes desativadas. Licença e hashes no projeto.

Casas mais legíveis: luz da lua.42→.62, névoa ambiente.009→.0065, varandas2.1→4.5 e7→12. O escuro foi preservado; fachadas aparecem melhor nas capturas reais.

Motor gravado existente.18→.34 (+5,52dB no ganho da fonte), passa-baixas1100→1800Hz, pitch e pausa/parada preservados. Não alteradas preferências pessoais de volume. Mix por escuta humana NÃO VERIFICADA.

## Rádio

Notícias existentes → três músicas KENTO MC24s cada → desliga sozinho → comentário “Até o rádio desligou com essas músicas ruins.” →6s de silêncio → rádio volta sozinho com1,6s de chiado gravado local → música estranha por44s, com reações de João → para ao estacionar na escola. O instrumental provisório anterior é mantido; não foi criada voz infantil.

Primeira reação3,5s, desconforto10s. Ao clicar no botão durante a fase estranha, ou automaticamente aos18s se o jogador não clicar, João tenta desligar e percebe que não funciona. Há clique de botão, a música continua e não reinicia. Flag aditiva hauntedPowerTried persiste a tentativa no checkpoint e é inválida antes da fase Song. Saves versão1/2 continuam aceitos, enum e história mantidos. Pausa congela chiado/música/timeline, reconstrução não repete as falas já passadas. As músicas normais ainda podem ser trocadas/desligadas pelo jogador, conforme o sistema anterior.

As quatro falas novas são LEGENDAS com campos de áudio preparados; não são novas gravações. Roteiro acompanha a entrega. Sem TTS.

## Arquivos

- Assets/RoadChapter/Scenes/RoadJourney.unity — alinhamento, postes, calçadas, visibilidade, motor e duração do rádio.
- Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionRoadRadio.cs — chiado de partida, duas reações e botão que não corta a música.
- BeforeDemolitionRoadFlow.cs — tentativa automática/manual e persistência do evento.
- BeforeDemolitionRoadState.cs — flag aditiva validada.
- Tests/PlayMode/BeforeDemolitionRoadSceneTests.cs —17 testes da cena, incluindo2novos casos completos.
- Tests/EditMode/BeforeDemolitionRoadSaveTests.cs —35 casos de save/compatibilidade, incluindo2novos.
- Assets/ThirdParty/RoadHaunt20261030/ — poste, materiais/texturas/LODs/licença/proveniência.
- Assets/CREDITS.md / Assets/ThirdParty/LICENCAS.txt — créditos novos.

## Provas e limites

52 testes únicos passaram: estrada17 + save35. O lote inicial tinha16PASS e uma asserção antiga de silêncio2s; corrigida para6s e o teste completo correspondente passou na execução focada. Resumo consolida o último resultado por nome, sem somar repetições. Testes reais cobrem: desligamento automático, chiado antes da música, pausa do chiado, falha do botão manual e automática, save/reload sem repetir tentativa, música persistindo e parada na chegada, modelos prontos/25luzes/pista alinhada, coleta/câmera/porta/portão e transição real para escola.

Capturas reais city-drive-forward.png e city-drive-side.png revisadas: pista/faixas coerentes e fachadas mais visíveis. Compilação final sem erros, auditoria nativa sem scripts/materiais ausentes. Story asset SHA256 idêntico ao backup. Save pessoal não acessado, testes temporários antes do Awake. Unity devolvida à cenaSchoolCEEP original, parada e limpa, sem override de save; preferências restauradas. Sem ZIP.

NÃO VERIFICADO: escuta humana da mix, conforto durante partida inteira ou build standalone. Dublagens novas e cantiga humana continuam pendentes.

Backups/scripts em work/RoadHaunt20261030. ImportLamp, PatchScene e Workspace.End JÁ executados; não reaplicar.
