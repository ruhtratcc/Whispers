# Entrega — Grampo da Helena / arrombamento opcional

Implementado na SchoolCEEP sem alterar a história principal. Dois arquivos no capítulo 3 e quatro no capítulo 4; nenhuma exigência nos capítulos 5/6. Coleta inicial na gaveta já existente, inventário/HUD/controles, interface 2D, sons, acessibilidade, apito, persistência e opções integrados. Zero grampos não impede terminar o capítulo principal.

## Arquivos

- Scripts/Lockpick/LockpickConfig.cs — configuração, modelo determinístico da tentativa, estado e preferências.
- Scripts/Lockpick/LockableDoor.cs — tranca opcional, proteção de portas com chave/pares/restrição e estado por ID.
- Scripts/Lockpick/LockpickPickup.cs — coleta inicial/reservas, limite e gaveta aberta.
- Scripts/Lockpick/LockpickSystem.cs — ciclo do minigame, som, pensamentos, dica do apito e bloqueios.
- Scripts/Lockpick/LockpickView.cs — uGUI/TMP, cilindro original, mouse, cancelar/pausar.
- Scripts/Lockpick/LockpickDialogue.cs — 63 falas editáveis e slots de voz.
- Scripts/Lockpick/LockpickNoteProposal.cs — cinco propostas não publicadas.
- Scripts/Equipment/EquipmentController.cs — Q condicional e lanterna opcional durante o minigame.
- Scripts/Equipment/EquipmentHud.cs — quantidade e SEM GRAMPO no slot 3.
- Scripts/Equipment/EquipmentOptionsPanel.cs — ajuda e abertura automática.
- Scripts/Interaction/InteractiveDoor.cs — delegação estreita apenas para nova tranca opcional.
- Scripts/Interaction/InteractiveDrawer.cs — mesma delegação, preservando movimento/chaves/eventos.
- Scripts/Systems/GameSession.cs — referência do novo sistema.
- Scripts/Systems/SaveSystem.cs — estado aditivo, schema existente e restauração após inventário/objetos.
- Scripts/Whistle/WhistleHighlightable.cs — exclui trancas opcionais dos contornos, conservando pilhas/objetos.
- Tests/EditMode/LockpickTests.cs — 13 casos, regras, dados, calibração, compatibilidade e proteção de chave.
- Tests/PlayMode/LockpickSceneTests.cs — três cenários nativos completos, coleta via E/Q, pausa, quebra, automático e checkpoints parciais.
- Tests/PlayMode/WhistleXrayPlayTests.cs — cobertura exclui os novos móveis-tranca; itens continuam cobertos.
- Data/Lockpick/ — configuração, diálogo, cinco propostas, cinco SoundSets, sprite original, grampo 3D e duas malhas compartilhadas do arquivo.
- Data/Equipment/GRAMPO.asset e Icons/clip.png — modelo e ícone do item.
- Assets/SchoolCEEP/Scenes/SchoolCEEP.unity — somente novos móveis/coletas/UI/sistema e referências; ordem preserva legendas acima do overlay.
- Assets/Audio/SFX/Lockpick/ — seis WAVs gravados/derivados e proveniência.
- Assets/Docs/Lockpick_Portas.md — IDs, recompensas, candidatos e portas principais verificadas.
- Assets/Docs/Lockpick_Anotacoes_Propostas.md — textos e fatos novos aguardando aprovação.
- Assets/Docs/Lockpick_ComoTestar.md — três passos para nova porta, controles, ajustes e checklist completo.
- Assets/Docs/Lockpick_Falas.csv e Dublagem/ — 44 falas ativadas + 19 novas, CSV/XLSX e roteiros por ator.
- Assets/CREDITS.md e Assets/ThirdParty/LICENCAS.txt — fontes/licenças/adaptações.

Os caminhos Scripts, Tests e Data acima são relativos a Assets/HorrorGameplay/. Nenhum ZIP ou build distribuível criado.

## Provas reais

Compilação final sem erros. 376 testes únicos passaram, zero falhas finais: 338 EditMode (13 novos), três PlayMode de arrombamento na escola, 15 de equipamento, 18 do apito, cobertura nativa dos objetos/pilhas e uma travessia principal do capítulo 3 sem grampo. Resultado bruto: tests.json. Foram corrigidos durante o passe: serialização de tipos Unity em arquivos separados; grampo inicialmente encoberto pela lancheira; testes que não esperavam a troca de equipamento/retorno de controle; teste de load que precisava reinicializar NarrativeDirector conforme contrato existente. Nenhuma alteração no diretor narrativo.

Auditoria por ID: 129 portas originais, zero mudanças de chave/tranca/par; zero IDs persistentes duplicados. As seis trancas novas são gavetas de móveis, sem NavMesh novo, sem destrancar salas restritas. final-audit.json, installed.json, preflight.json e mesh-proof.json documentam a instalação. Banco de história permanece sem referências a lockpick. Save pessoal não usado; testes usam diretórios descartáveis.

Captura training-ui.png: render real de uma cópia da UI carregada da cena, 1920×1080, revisado visualmente; NÃO é uma foto de uma campanha inteira nem valida sensação de mouse. Planilha renderizada em sheet-before/after.png; zero erros de fórmula detectados na inspeção, 912 IDs.

## Calibração e custo

Bot com busca simulada de 3/5/7 s por pino, depois tensão perfeita: fácil 8.55 s; média 21.00 s; difícil 37.50 s, 30 amostras por dificuldade, zero quebras nesse bot. Bot mantendo ângulo errado por 10 s: duas quebras por dificuldade. Isso testa regras e tempos, não a habilidade de um jogador.

ProfilerRecorder mediu frames nativos contendo um ciclo fácil completo: média 21933 bytes, baseline 15325 bytes; amostras [22044, 21885, 21912, 21893, 21932]. Inclui fundo da cena/UI/áudio/testes e agendamento de save. NÃO é custo isolado exato por arrombamento. GC.GetAllocatedBytesForCurrentThread retornou zero até para uma alocação-controle de 4096 bytes; a API é inadequada neste Editor e seus zeros NÃO comprovam ausência de alocação. Custo isolado exato: NÃO VERIFICADO. Não há busca global de objetos nos Updates novos.

## Pendências e decisões

- Cinco notas PROPOSTA — APROVAR, fora do jogo; todos os fatos ambientais novos marcados. Por isso o treino entrega a pilha agora, mas sua nota ainda não. A diretoria tem maior recompensa de recursos; sua recompensa narrativa depende de aprovação.
- 63 vozes pendentes: legendas provisórias e roteiros prontos. Mix/espacialidade percebida, cursor em mouse real, câmera/lanterna em uso prolongado: NÃO VERIFICADO por pessoa.
- Campanha completa jogada manualmente, versão standalone e todos os monitores/aspectos da nova interface: NÃO VERIFICADO. Compilação e testes específicos são reais; não equivalem a esses testes humanos.
- Áudio de quebra usa impacto metálico representativo; gravação literal seria acabamento opcional. Assets locais reutilizados, somente um WAV CC0 baixado; créditos preservados.
- Preservados FOV 49, save pessoal, história antiga arquivada, PC retrô/celular e terror sem perseguição/ataque. Não foram abertas salas bloqueadas só para encaixar a mecânica.

Controles e teste rápido: pegar o grampo na secretaria, equipar 3/roda/Q, usar E no arquivo de treino, mover o mouse e segurar clique. Esc cancela; Pausar/F10 pausa. Opções > Equipamentos contém os dois modos de acessibilidade.

Estado final: Unity parada na SchoolCEEP, cena limpa, sem compilação, sem override de teste nem escrita pendente; execução em segundo plano restaurada para false. Seis trancas e 63 falas carregadas após restauração; legendas acima do overlay confirmadas.
