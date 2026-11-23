# Apito do Luan — entrega de 31/10/2026

Implementação integrada às três cenas atuais do projeto. Um serviço por cena; 3 respondedores no escritório e 13 na escola. Compilação concluída sem erros; **72 testes únicos aprovados**, sem somar reexecuções. Unity devolvida à SchoolCEEP, parada, limpa e sem redirecionamento de save. Testes usaram diretórios descartáveis antes do Awake; seu perfil pessoal não foi usado nesta entrega.

## Controles e configuração

- **Q** equipa/guarda. **Botão do meio do mouse**: toque rápido; manter pressionado produz sopro longo. Gamepad: direcional esquerdo / botão superior direito.
- Primeira execução: hesitação de 1,25 s; seguintes: subida de 0,22 s. Limiar do longo: 0,55 s; duração máxima: 3 s. Cooldown de 4,5 s, inclusive se guardar o apito para interromper a animação.
- Editar `Assets/HorrorGameplay/Data/Whistle/WhistleConfig.asset`: `range` (24 m), `cooldown`, `holdMinimum/holdMaximum`, `mirrorHoldSeconds` (2,1 s), volumes, atraso, `stressThreshold` (3), ganho e resfriamento de tensão.
- Pausa, PC, celular, inspeção, diálogos bloqueantes e blackout impedem o uso. Dentro do carro fica bloqueado; após sair funciona. Nenhuma fase do rádio foi alterada.
- Dica discreta após 20 s sem aprender, por 3,5 s, salva uma vez. Pode desligar em Opções/Acessibilidade → Dicas de controles do apito. As falas não citam teclas.

## O que foi integrado

1. Escritório: reação à coleta existente; resposta muito fraca e única do mesmo armário do email 7; garrafa vibra e tilinta ao sopro longo, sem quebrar. Pilha opcional atrás das pastas sobre a mesa de arquivo; responde ao apito, entra no inventário e é usada uma vez na escola quando a carga cai a 20% ou menos, restaurando até 120 s de carga. Requisitos de saída — emails 1/6/7 e apito — preservados.
2. Secretaria/biblioteca: respostas da lancheira, chave, relatório e três portas; sons ocos/mais densos configurados por porta. Itens deixam de responder após coleta/abertura relevante.
3. Quatro alunos: papel do caderno, pequeno som metálico do boné/marmita e clique da fita. **Nenhuma aparição infantil responde.**
4. Duas vozes: eco curto vindo do lado correto, sem retorno no lado errado, variação de atraso conforme contador de voltas e respostas raras de Helena. A porta correta existente permanece no mesmo lugar; o apito não pula a entrada na sala.
5. Espelho: toque curto dá interferência leve; só sopro longo acumula trincas e aciona a quebra existente. Três camadas de fissuras acompanham o progresso salvo. Examinar informa o uso longo; objetivo antigo foi corrigido. Recusar/Assinar e seus finais permanecem.
6. Tensão própria do apito: cresce com insistência, esfria com o tempo, altera discretamente lanterna, atraso e respiração. Ganho e impacto reduzidos nos capítulos 5/6. **Não reduz sanidade, não causa dano ou blackout.**
7. Pool/mixers/oclusão/reverb existentes reutilizados. Sem criação de AudioSource por sopro, sem busca global no Update. Pulso de vinheta e de material sem emissão; Reduzir efeitos elimina os pulsos e a vibração visual. Sem radar ou novo ícone de alvo.
8. Save aditivo: aprendizado, contagem, cooldown, tensão, dica, pilha e respondedores acionados; dados antigos continuam válidos. IDs de itens/flags de história preservados. A fachada legada do apito segue compatível com cenas arquivadas, sem duplicar entrada nas três cenas atuais.

## Arquivos criados/alterados

Caminhos abaixo relativos a `C:/Users/ruhtra/My project/`.

- `Assets/HorrorGameplay/Scripts/Whistle/WhistleSystem.cs` — entrada, animação, estados, feedback e integração.
- `Assets/HorrorGameplay/Scripts/Whistle/WhistleResponder.cs` — alcance/cone, condições, som, pulso e evento configuráveis.
- `Assets/HorrorGameplay/Scripts/Whistle/WhistleConfig.cs` — configuração, contexto e interface `IWhistleResponsive`.
- `Assets/HorrorGameplay/Scripts/Whistle/WhistleState.cs` — estado validado, cópia e resfriamento.
- `Assets/HorrorGameplay/Scripts/Whistle/WhistleDialogueCatalog.cs` — catálogo editável de texto/ID/gatilho/prioridade/ator/direção/áudio.
- `Assets/HorrorGameplay/Scripts/Systems/InputReader.cs` — ações livres adicionadas ao mapa clonado em runtime.
- `Assets/HorrorGameplay/Scripts/Systems/SaveSystem.cs` — captura/restauração aditiva do estado novo.
- `Assets/HorrorGameplay/Scripts/Interaction/Inventory.cs` — remoção da pilha opcional ao consumi-la.
- `Assets/HorrorGameplay/Scripts/Audio/AudioManager.cs` — camada discreta de respiração com gravação existente.
- `Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionWhistle.cs` — fachada compatível para os consumidores existentes.
- `Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionMirrorFlow.cs` — acúmulo de trincas e quebra pelo longo.
- `Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionContent.cs` — objetivo atualizado do espelho para futuras gerações.
- `Assets/HorrorGameplay/Scripts/UI/AccessibilityPanel.cs` — opção de dicas e espaço para o novo controle.
- `Assets/HorrorGameplay/Data/Whistle/` — config, catálogo de 58 falas, eco gravado, prefab do apito e material das trincas.
- `Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionStory.asset` — texto do objetivo do espelho; IDs e sequência preservados.
- `Assets/Audio/SFX/Whistle/` — dois recortes de áudio gravado, importados como mono.
- `Assets/ChapterOneOffice/Scenes/Chapter1Office.unity` — serviço, respondedores e pilha opcional.
- `Assets/RoadChapter/Scenes/RoadJourney.unity` — serviço com bloqueio durante a direção.
- `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity` — serviço, alvos e trincas do espelho.
- `Assets/HorrorGameplay/Tests/EditMode/WhistleEditTests.cs` — validação/configuração/compatibilidade/alcance/cone.
- `Assets/HorrorGameplay/Tests/PlayMode/WhistlePlayTests.cs` — estados, tap/hold, pausa, save, custo, espelho e medição isolada.
- `Assets/HorrorGameplay/Tests/PlayMode/WhistleOfficeSceneTests.cs` — coleta real, PC, armário, garrafa e pilha.
- `Assets/HorrorGameplay/Tests/PlayMode/WhistleSchoolSceneTests.cs` — alvos reais, alunos, voltas, sanidade e reserva.
- `Assets/HorrorGameplay/Tests/PlayMode/WhistleRoadSceneTests.cs` — carro e celular reais.
- `Assets/HorrorGameplay/Tests/PlayMode/BeforeDemolitionOfficeSceneTests.cs` — classe parcial para reutilizar o fixture protegido.
- `Assets/HorrorGameplay/Tests/PlayMode/BeforeDemolitionEndingSceneTests.cs` — teste passa a usar sopro longo nos finais.
- `Assets/Docs/Dublagem/Dublagem_MESTRE.csv` — 58 entradas acrescentadas, 805 anteriores preservadas.
- `Assets/Docs/Dublagem/Dublagem_MESTRE.xlsx` — mesmas 58 entradas; resumo estendido para 863 IDs; outras abas preservadas em valores.
- `Assets/Docs/Dublagem/Roteiro_POR_DUBLADOR/Homem1.md` — 52 falas novas de João.
- `Assets/Docs/Dublagem/Roteiro_POR_DUBLADOR/Mulher_idosa.md` — 6 falas novas de Helena.
- `Assets/ThirdParty/LICENCAS.txt` — procedência das derivações e reuso local.
- `Assets/Docs/Apito_20261031_ENTREGA.md` — esta documentação.

## Adicionar um respondedor em três passos

1. No objeto real, adicionar `WhistleResponder`, definir um `responderId` único e o capítulo permitido. Não adicionar à criança.
2. Escolher toque/longo, alcance (0 herda config), ângulo (360 ignora direção), cooldown, condição `requiredFlag`, `disabledAfterFlag` ou `hiddenWhenItemOwned`, e o SoundSet gravado de resposta. Marcar `onceOnly` para resposta única.
3. Atribuir renderers/objeto vibrante e `onResponse` se necessário. `signalOnResponse` só aceita o próximo marco já previsto pelo NarrativeDirector. Testar de perto, longe, de costas e após salvar/carregar.

## Checklist guiado no Play

- Cap. 1: ler email 6, abrir a segunda gaveta e concluir a inspeção do apito. Conferir reação; aguardar 20 s sem usar para a dica. Equipar, clicar rapidamente, observar hesitação e resposta única do armário. Segurar e observar garrafa; pegar a pilha escondida atrás das pastas. Confirmar que o escritório ainda exige seus emails para sair.
- Cap. 2: tentar no carro/telefone aberto — bloqueado; sair explicitamente pela porta e usar fora. O rádio mantém a sequência anterior.
- Cap. 3: procurar lancheira/chave/relatório pelo som; conferir abafamento atrás de parede e recolher itens. Com pilha opcional e carga baixa, conferir troca única. Abrir inspeção/PC/celular/pausa: sem novos sopros.
- Cap. 4: testar perto de cada objeto e longe deles; recolher e repetir. Observar que as crianças não respondem.
- Cap. 5: ouvir o eco do lado correto em cada volta; olhar para o lado errado e comparar a ausência de retorno. Helena só comenta raramente; entrar na sala continua obrigatório.
- Cap. 6: observar a aparição prevista, dar toque curto e confirmar espelho inteiro. Segurar aproximadamente 2,1 s de som sustentado, observar trincas e quebra. Testar Recusar e Assinar, salvar/recarregar e conferir a escolha.
- Insistir: aumento leve de respiração/atraso/falha visual; aguardar resfriamento. Guardar o apito não burla cooldown. Não deve perder sanidade por isso.
- Acessibilidade: desligar dicas e Reduzir efeitos; confirmar que o som e as regras continuam, sem pulsos visuais. Reabrir um save antigo e um novo.

## Sons e dublagem

**Nenhum download novo. Nenhuma síntese ou TTS.** Reutilizada gravação CC0 de Joseph SARDIN, https://bigsoundbank.com/plastic-whistle-s1017.html, já presente como `whistle_real.wav`. Recorte de 0,30 s para toque e trecho gravado com crossfade de 0,67 s para loop longo. Original preservado; hashes/durações em `sound-provenance.json`. Demais respostas usam SoundSets locais já licenciados de vidro, metal, papel e fita. O modelo Poly by Google mantém o crédito CC-BY 3.0 já existente.

**58 falas novas: 52 João / 6 Helena**, com 4 variações por categoria de João e 3 por categoria de Helena. Catálogo editável no Inspector; os campos `voice` ficam vazios, aguardando gravação humana. TXT completo: `Apito - falas para gravar.txt`. Os sons de objetos estão ligados; as novas vozes, por enquanto, são legendas. Não há SFX obrigatório em falta. Para refinamento sonoro opcional futuro: “small metal whistle short blow” em freesound.org e “glass bottle ring” em pixabay.com/sound-effects, após escuta comparativa.

## Evidências e limites

`test-summary.json` reúne **72 testes únicos / 72 aprovados / 0 falhas**: 11 EditMode do apito; 11 PlayMode focados; 1 reserva; 35 regressões de save; 8 espelho; 4 corredor; 2 finais reais com reload. Os fixtures nativos protegem o perfil antes da cena carregar. Os testes usam posicionamento controlado e tempos reduzidos quando indicado: não equivalem a uma caminhada humana completa.

`native-audit.json`: um serviço por cena, 16 respondedores no total, IDs sem duplicação, nenhum script ausente e nenhum collider no prop de mão. `final-editor-state.json`: SchoolCEEP, Play desligado, cena limpa, background original restaurado, override de teste nulo.

Medição final no Editor, 100 sopros síncronos aquecidos na escola real com HUD, pool e 13 respondedores registrados: **0 B médios / 0 B de pico de alocação gerenciada; 0.007143 ms médios / 0.045400 ms de pico**. Não inclui respostas diferidas, I/O de save nem GPU/quadro completo; cooldown foi zerado exclusivamente no benchmark. Uma medição anterior no mesmo Editor teve pico isolado de 9,737 ms; não se promete estabilidade de hardware com estes microtestes. Medição isolada do despacho também em `performance.json`. Sem FPS estimado.

**NÃO VERIFICADO:** mixagem por ouvido, posição/força percebida dos ecos, naturalidade e sincronização das vozes futuras, sensação da animação/onda/trincas em todos os monitores, painel em todas as resoluções, performance no executável e em hardware do jogador. Capturas automáticas dos finais estão em `Endings/`; a captura antiga do sorriso antecede a correção do texto do objetivo. Não há build/ZIP novo.

Decisões: carro bloqueia apito enquanto sentado; pilha é opcional e consumida automaticamente uma única vez na escola; respostas dos alunos são sons de objetos gravados; direção correta existente não muda de porta; efeitos do apito não mexem na sanidade. Não foram acrescentados fatos de história, personagens, combate, perseguição ou ataque. Pendência de produção: gravar as 58 vozes e fazer a escuta humana do conjunto.
