# Celular, acessos, árvores, espelho e conclusão dos SFX — 05/11/2026

Concluído. Celular sem digitação automática, conversa abre ao chegar mensagem e aguarda pausa/inspeção/PC/cena bloqueada terminar. No carro, uma mensagem abre a conversa mesmo com uma ligação pendente; a abertura manual preserva a prioridade da chamada. Estado antigo composerUsed continua compatível.

Grade original bd.door.stairs transferida do corredor superior para a base da escada central, ajustada à largura do vão. Duas portas da rampa (school.door.0.81 e school.door.1.32) usam bd.stairs.key; sem chave, não contornam a grade. Objetivo: “Procure a chave no refeitório.” Chave e progressão preservadas.

82 árvores visíveis substituídas:14 na escola e68 na estrada. Reuso do modelo CC0 Tree Small02 da Poly Haven. A revisão visual encontrou deformação na simplificação antiga: galhos/tronco originais preservados e folhas recompostas em cartões com o atlas original. Dois LODs, fontes de reimportação sincronizadas, nenhum novo download. Troncos de obstrução caídos preservados.

O espelho revela um vão recortado em oito malhas existentes, com UVs conservados, colisões divididas e espaço fechado com profundidade atrás da parede. Moldura antiga em formato de porta retirada; entrada lateral da antiga sala (school.door.1.10) fechada com alvenaria, conservando ID. Interação após a revelação passa aos pertences, permitindo aproximar sem uma parede invisível.

## Verificação real

-35 testes únicos aprovados:7 PlayMode (2 acessos/final real,3 celular estrada/escola/corredor,1 celular/PC e1 final ruim),14 Editor de áudio e14 Python.
-Final bom real na SchoolCEEP:78,10s, cutscene → amanhecer → PC/email → resposta → créditos, uma conclusão, FOV49, save de teste válido. Nenhum travamento reproduzido. Usuário esclareceu que a parada poderia ter sido uma saída do Play para edição; não foi inventada uma causa de crash.
-Colisão real da cápsula antes/depois de abrir a grade; rampa bloqueada sem chave; checkpoint anterior restaura grade/portas fechadas.
-Auditoria nativa:180 IDs da escola e1 da estrada conservados;0 scripts ausentes,0 shaders quebrados e0 visuais antigos de árvores ativos.
-Compilação sem erros; Console final0 erros. Editor restaurado à SchoolCEEP limpa e parada, sem override de save. Save pessoal não acessado. Nenhum ZIP/build.
-Falhas intermediárias foram guardadas: abertura automática no carro priorizava a ligação (corrigido com conversationOnly), e o teste tentava clicar num botão propositalmente desativado (fixture corrigida). Primeira auditoria ignorava118 overrides de IDs de prefabs; auditoria corrigida comparou os180 IDs. Nada foi apagado para esconder essas tentativas.

## Sons

Conta nova conectada após pedido explícito; primeira autorização recusada, segunda aprovada. Saldo verificado10.000.66 variantes faltantes geradas, resultando em162 sons/314 variantes completos. Custo desta rodada3.570 créditos; saldo final verificado6.430. Histórico anterior preservado, sem apagar recibos ou liberar teto artificialmente.

314 arquivos conferidos por hash/decodificação/pico/RMS;33 loops sem emenda suspeita;1.228,914s processados;0 mudanças nos prompts;0 erros técnicos. Catálogo nativo162 entradas/314 clipes/69 ligações de slots,0 novos nulos, sem referências aos brutos no Player. Demais sons disponíveis no catálogo; não foram inventados gatilhos para objetos inexistentes. Plano Free: uso não comercial e atribuição; LICENCAS.txt atualizado. Nenhum TTS ou voz humana fabricada.

## Arquivos principais

-Assets/HorrorGameplay/Scripts/Phone/PhoneMessages.cs, PhoneMessagesView.cs, PhoneMessageCatalog.cs — abertura e compositor.
-Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionFourStudentsFlow.cs — chave, rampa e objetivo.
-Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionMirrorFlow.cs — alvo de interação após a abertura.
-Assets/SchoolCEEP/Scenes/SchoolCEEP.unity e Assets/RoadChapter/Scenes/RoadJourney.unity — alterações nativas.
-Assets/SchoolCEEP/Meshes/Access20261105/ e Assets/models/Polish20261103/tree_small_02/ — abertura e árvores.
-Assets/HorrorGameplay/Tests/PlayMode/SchoolAccess20261105Tests.cs e fixtures de celular atualizadas — regressões.
-Assets/Audio/, Assets/HorrorGameplay/Data/SfxBatch/, Assets/Docs/Audio/sfx_manifest.json — sons e proveniência.
-Backups:Assets/_Substituidos/SchoolAccess20261105/ e SfxResume20261105/.

## Como conferir

No Play em perfil de teste, aguarde mensagem e feche com P/Esc; clique na barra de mensagem e confirme que ela não escreve. No capítulo4, confira objetivo, grade na escada, portas da rampa e coleta da mesma chave. No final, recuse, examine a abertura, devolva o apito e acompanhe a cutscene e o email.

Imagens stairs-after.png, mirror-opening-after.png e school-tree-after.png usam iluminação temporária de diagnóstico, não a iluminação do jogo. Provas JSON e native-ending-trace.txt ficam nesta pasta.

## NÃO VERIFICADO

Player standalone atualizado, campanha inteira com os novos acessos, escuta humana/qualidade artística do mix e desempenho no PC de outro jogador. Novas falas que já estavam sem gravação continuam com legendas provisórias; concluir os efeitos sonoros não significa gravar dublagem ausente.
