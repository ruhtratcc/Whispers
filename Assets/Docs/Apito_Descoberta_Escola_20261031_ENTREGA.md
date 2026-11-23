# Apito — descoberta só na escola

Revisão concluída em 31/10/2026 no projeto `C:/Users/ruhtra/My project`. **87 testes únicos aprovados**, compilação sem erros, três shaders suportados/sem erros e prova de imagem da GPU. Unity devolvida à SchoolCEEP, limpa, parada, sem override de testes. Seu perfil pessoal não foi usado; os testes redirecionam saves antes do Awake. Sem ZIP.

## O que mudou

- Mantidos WhistleSystem, IWhistleResponsive, WhistleResponder e WhistleConfig. Q equipa/guarda; botão do meio toca/segura. Apito comum no escritório/estrada, sem efeitos, respostas ou custo. A garrafa, armário e pilha do escritório não reagem mais; os três respondedores anteriores estão desativados. Dicas de uso e hesitação de descoberta saíram do escritório. Coleta e requisitos de saída existentes foram preservados.
- Na secretaria, a aproximação dispara uma única legenda/sussurro previsto “o apito”. O campo de voz humana está vazio e identificado para gravação; não foi colocado um som falso no lugar da fala.
- Três folhas novas na parede da biblioteca: desenho, anotação exata da Helena e aviso rabiscado exato. Inspeção e Anotações existentes reutilizadas. Ler um dos dois primeiros documentos grava `whistle.lore_known`; o terceiro só adverte. A biblioteca aguarda essa leitura para concluir seu capítulo, com lembrete breve se tentar sair antes. IDs e marcos da história não foram renomeados.
- Antes da leitura, soprar continua comum, inclusive na escola. Após a leitura, a primeira execução hesita por 1,25 s, registra `whistle.discovered` e libera o efeito. Quatro respostas breves são previstas em posições 3D distintas, escolhidas ao redor do local desse primeiro sopro e mantidas fixas durante o evento; depois João reage. As vozes aguardam gravação. A dica de controle aparece uma vez depois da leitura, pode ser desativada e some após usar.
- Atualização 03/11: o primeiro toque curto na escola revela por instantes quatro rastros de pegadas até a escada e destaca a chave na gaveta. Sem resposta de vidro. A lancheira preserva sua interação e seus marcos de progresso.
- O pulso segue a geometria usando profundidade da URP, expande em 1,1 s e o feedback dura 3,6 s com fades. Não há blur global. A dessaturação é relativa ao perfil existente: base −20 → alvo −32 no pico; a vinheta sobe apenas 0,025 sobre a base. HUD/PC/celular não recebem o novo passe.
- 23 alvos configurados: azul claro listrado para itens úteis, âmbar sólido para história, branco-esverdeado tracejado para passagens. Cor e padrão diferenciam as categorias. Três partículas mínimas em cache aparecem junto a itens de história visíveis; somem quando ocultos ou no modo reduzido. Objetos coletados deixam de acender. Em gaveta fechada, destaca-se o móvel; aberta, o item.
- Itens úteis/passagens exigem linha de visão. Só história pode mostrar silhueta muito fraca através de parede, até 6 m. A consulta usa OverlapSphereNonAlloc numa camada exclusiva, buffer de 128 e limite de 24 destaques; não varre os milhares de objetos do mapa a cada quadro. Malhas, partículas e materiais são preparados uma vez.
- Cinco rotas de rastros: quatro objetos dos alunos e o acesso correto no corredor. Ao soprar, aparece a rota elegível mais próxima, com sete pegadas e uma marca de mão. As 35 pegadas e cinco mãos foram alinhadas às superfícies; seus Transforms são editáveis por objetivo. Não há pathfinding ou indicação do trajeto inteiro da escola. No corredor, o cone correto foi reduzido a 40° para não revelar o caminho ao olhar para Helena. O contador de voltas continua variando o eco; a porta correta existente permanece fixa.
- Crianças paradas/aparições não recebem WhistleResponder nem WhistleHighlightable. Os objetos dos alunos têm respostas distintas: papel, batida abafada, apito baixo e chiado. A resposta de chiado é limitada a 1,1 s com fade de 150 ms; o arquivo original de 11,7 s não foi alterado.
- O espelho mantém a lógica de sopro longo, trincas progressivas agora frias e a quebra existente; o curto não quebra. Recusar/Assinar e finais foram preservados. Custo psicológico continua separado da sanidade, sem dano, ataque ou perseguição, e reduzido nos capítulos 5/6.
- “Reduzir efeitos” desliga onda, dessaturação, partículas e rastros, mantendo contornos suaves. Inclusive ao alterar a opção durante um efeito. Restauração anterior à descoberta cancela sussurros/comentários pendentes daquela tentativa.

## Saves e compatibilidade

O estado do apito agora tem versão 2; o save principal continua em sua versão anterior. As flags novas são aliases do estado salvo: `loreKnown` ↔ `whistle.lore_known`, `discovered` ↔ `whistle.discovered`, além de tentativa fora, sussurro da secretaria e hesitação escolar. Campos antigos continuam válidos. Saves antigos já nos capítulos 4–6 recebem migração de aprendizado para evitar bloquear o espelho; saves novos sem leitura não ganham descoberta automaticamente. Os IDs de itens e flags anteriores foram mantidos. Leituras novas usam IDs próprios `bd.whistle.lore.0/1/2`.

## Arquivos criados/alterados

A lista completa, **um arquivo por linha**, está em `ARQUIVOS.txt`. Principais mudanças:

- `Scripts/Whistle/WhistleSystem.cs` — gate escolar, descoberta, estado, som comum e feedback relativo ao perfil.
- `Scripts/Whistle/WhistleResponder.cs` — gate de descoberta, respostas infantis e duração/fade limitados.
- `Scripts/Whistle/WhistleState.cs` — estado v2 e cópia compatível.
- `Scripts/Whistle/WhistleConfig.cs` — cores, alcance, duração, custo, máscaras e materiais.
- `Scripts/Whistle/WhistleHighlightable.cs` — contornos, silhueta, conteúdo oculto e partículas em cache.
- `Scripts/Whistle/WhistleSense.cs` — consulta limitada, oclusão e seleção da rota.
- `Scripts/Whistle/WhistleTrailRoute.cs` — rastros por capítulo/item, fades e acessibilidade.
- `Scripts/Whistle/WhistleLoreClue.cs` — leitura pelo sistema de inspeção existente.
- `Scripts/Whistle/WhistleSchoolDiscovery.cs` — sussurro, respostas iniciais e lembrete de leitura.
- `Scripts/Whistle/WhistleSenseCamera.cs` — câmera escolar explicitamente habilitada para o passe.
- `Scripts/Whistle/WhistlePulseRendererFeature.cs` — passe URP Render Graph com profundidade.
- `Shaders/WhistlePulse.shader` — anel espacial na geometria, sem blur.
- `Shaders/WhistleOutline.shader` — contornos por categoria e silhueta limitada.
- `Shaders/WhistleTraces.shader` — rastros/partículas suaves.
- `Scripts/Narrative/BeforeDemolitionSchoolFlow.cs` — conclusão da biblioteca espera a leitura de uma pista.
- `Assets/HorrorGameplay/Data/BeforeDemolition/School/SchoolItems.asset` — três itens inspecionáveis acrescentados.
- `Assets/Settings/PC_Renderer.asset` e `Mobile_Renderer.asset` — uma feature por renderer, sem duplicação.
- `ProjectSettings/TagManager.asset` — camada 30, WhistleTargets.
- Escritório e SchoolCEEP — ajustes de cena; a cena da estrada foi preservada.
- `Assets/HorrorGameplay/Data/Whistle/Discovery/` — três imagens provisórias, seus modelos/dados/materiais e malhas dos rastros.
- Planilhas CSV/XLSX e roteiros por dublador — linhas novas e antigas explicitamente substituídas.
- Testes atuais e novos — progressão, feedback, save, GPU, cenas reais e regressões.

Os caminhos `Scripts` e `Shaders` acima partem de `Assets/HorrorGameplay/`. O restante usa o caminho completo relativo ao projeto.

## Ajustes no Inspector

Abrir `Assets/HorrorGameplay/Data/Whistle/WhistleConfig.asset`:

- `range` 24 m, `cooldown` 4,5 s; `holdMinimum` 0,55 s, `holdMaximum` 3 s e `mirrorHoldSeconds` 2,1 s.
- `effectDuration` 3,6 s; `pulseExpansionSeconds` 1,1 s (mínimo 0,6); `desaturation` é redução relativa, sem alterar o perfil base.
- `usefulColor/storyColor/pathColor`, `highlightStrength`, `outlineWidth`; os padrões também diferenciam categorias.
- `storyThroughWallRange` 6 m e `throughWallStrength` 0,035; úteis e passagens não atravessam paredes.
- `maxHighlights` 24, `highlightMask` WhistleTargets. Capacidade fixa da consulta: 128.
- `stressThreshold` 3, `stressPerBlow`, `stressCoolingPerSecond` e limites controlam apenas a tensão do apito.
- `responseMaxSeconds` 1,1 s limita respostas de objetos; não corta o sopro longo do jogador.

### Novo WhistleHighlightable em três passos

1. Adicionar o componente ao objeto, definir ID único, categoria, capítulo, flag necessária e item cuja coleta o oculta.
2. Atribuir os Renderers originais e os materiais/Quad do preset existente; criar um filho com SphereCollider trigger na camada **WhistleTargets**. Para conteúdo escondido, indicar `concealingDrawer` e `concealedTargets` do móvel.
3. Ajustar alcance/visibilidade e, quando fizer sentido, `directionGate`. Testar perto/longe, atrás de parede e após coletar. Não adicionar às aparições.

### Novo WhistleResponder em três passos

1. Adicionar ao objeto, ID único e capítulo; escolher toque/longo, alcance e cone.
2. Atribuir SoundSet gravado, condição/ocultação, onceOnly/cooldown e, se aplicável, gatilho de fala do catálogo. `signalOnResponse` só aceita um próximo marco já previsto.
3. Testar direção, oclusão, pausa e restauração. Fora da escola e antes da descoberta, o serviço impede as respostas.

## Checklist guiado no Play

1. **Escritório:** pegar o apito normalmente; aguardar e usar Q/botão do meio. Som comum, sem dica, custo ou reação do armário/garrafa/pilha. A tentativa fora fica salva. PC aberto/inspeção/pausa bloqueiam.
2. **Estrada:** dentro do carro permanece bloqueado para preservar o rádio; fora, som comum. Celular aberto bloqueia.
3. **Secretaria:** aproximar-se da lancheira e conferir o sussurro previsto uma vez. Antes de ler as pistas, apito comum. Encontrar chave e seguir à biblioteca normalmente.
4. **Biblioteca:** inspecionar desenho ou anotação, conferir texto no painel Ler e nas Anotações. O rabisco inferior sozinho não ensina. Sem ler uma das duas pistas, a conclusão do capítulo aguarda e lembra do mural. Depois da leitura, aparece a dica uma vez. Primeiro sopro: hesitação, onda, respostas previstas e objetos próximos; repetir não refaz a descoberta.
5. **Pegadas/pilhas:** na secretaria, tocar o apito pela primeira vez na escola. Conferir o branco fraco nos rastros até a escada e o destaque temporário da chave. Depois examinar a pista, abrir a gaveta, ler os desenhos da lancheira e recolher a chave. Conferir pilhas sem alterar suas interações.
6. **Quatro alunos:** cada objeto âmbar responde de forma própria; seguir rastros do mais próximo ainda não coletado. As aparições nunca acendem/respondem.
7. **Corredor:** testar cada volta mirando o acesso lateral e depois Helena. O lado errado não mostra rastros nem eco correto; a entrada na sala continua obrigatória.
8. **Espelho:** curto não trinca; longo acumula fissuras frias e quebra após o tempo necessário. Testar Recusar e Assinar, salvar e recarregar.
9. **Custo/acessibilidade:** repetir usos e deixar esfriar; conferir respiração/falha leve sem perda de sanidade. Ativar Reduzir efeitos antes e durante o pulso: só contornos suaves. Salvar/carregar antes e depois da descoberta; um retorno anterior não toca respostas pendentes.

## Áudio, falas e imagens

Nenhum download novo ou áudio sintetizado. Foram reutilizadas gravações locais já licenciadas do apito, vidro, papel, madeira, chiado e respiração. Pool, mixers, oclusão e reverb existentes continuam em uso.

**29 falas ativas novas + 1 proposta opcional “REGRAVAR?”**. Doze falas antigas de primeiro uso/armário/garrafa foram substituídas e marcadas como tal na planilha; o catálogo ativo tem 75 linhas, incluindo as reaproveitadas. **As 75 vozes do catálogo do apito aguardam gravação humana**: atualmente esses trechos usam legendas e campos de áudio vazios. Não confundir os SFX de resposta, que estão ligados, com dublagem já pronta.

- Roteiro novo: `Apito - falas novas e revisadas.txt`.
- Master atualizado: CSV/XLSX em `Assets/Docs/Dublagem/`; 893 IDs, incluindo 12 históricos substituídos. As estimativas de gravação excluem essas substituições.
- Roteiros individuais: Homem1, Homem2, Homem3, Menina1, Menina2 e Mulher_idosa.
- Fita 1: a linha opcional só está no roteiro/planilha. **Seu texto e sua gravação não foram substituídos.** O padrão é explicar o sinal por escrito.
- Faltam vozes humanas: “o apito”, quatro respostas iniciais, respostas curtas por criança e falas novas de João. Não foi usada uma voz externa sem corresponder ao texto. Para pesquisa opcional de SFX: “quiet pencil scratch”, “soft wood knock”, “small metal whistle” em freesound.org; “cassette tape hiss” em pixabay.com/sound-effects. Os SFX necessários já têm gravação local; esses termos são apenas refinamento futuro.

Os três slots de imagem estão nos InspectionItemData LuanDrawing/HelenaNote/ScribbledWarning em `Data/Whistle/Discovery`. Placeholders originais **768×1024, proporção 3:4**, claramente identificados, estão ligados à inspeção e aos props. Texto limpo permanece no painel Ler. `PROMPTS-IMAGENS.txt` entrega os três prompts em inglês e a proporção; a arte final ainda pode substituí-los sem alterar a mecânica.

## Testes, custo e limites

`test-summary.json`: **87 casos únicos / 87 aprovados / zero falhas finais**, incluindo 11 EditMode do apito, 35 de compatibilidade de save, 14 casos de mecânica/visibilidade/GPU, cenas reais, seis regressões adicionais da escola, os dois finais com reload e testes legados do corredor/espelho. Os dois diagnósticos iniciais foram corrigidos: contagem de alvo desatualizada e cone amplo que revelava rastros na direção errada. Não foram escondidos por repetir contagens; os resultados brutos permanecem nos JSONs, e o resumo usa a última execução de cada caso.

A prova GPU compara pixels de baseline/onda/contorno: diferença média do anel **0.018573**, contorno **0.001522** na imagem de teste. `gpu-*.png` prova renderização, não a sensação final. A captura `library-discovery.png` mostra a cena real com as folhas provisórias.

Medição na escola real, **64 sopros aquecidos**, consulta limitada e alvos ativos: **0 B médios / 0 B de pico de alocação gerenciada; 0.065641 ms médios / 0.085500 ms de pico**. Primeiro uso na medição final: 0.301 ms; outra execução teve primeiro uso de 6,618 ms. Não houve crescimento de renderers ou AudioSources entre usos. A amostra final encontrou 5 marcadores e destacou 4, sem saturar o buffer. Inclui o caminho síncrono do apito/consulta/contornos/pool; exclui respostas diferidas, frames de fade, I/O e tempo de GPU/quadro inteiro. Cooldown foi zerado só no benchmark. Não se estima FPS ou custo total do jogo a partir disso.

`native-audit.json`: três cenas sem scripts ausentes, uma feature por renderer PC/Mobile, zero erros de shader e nenhuma aparição com respondedor/destaque. `final-editor-state.json` confirma SchoolCEEP parada/limpa e override de testes nulo.

**NÃO VERIFICADO:** aparência final e sensação no seu monitor/hardware, mixagem por ouvido, naturalidade/sincronia das gravações futuras, caminhada humana completa e performance do executável/GPU em PCs de terceiros. Os testes usam posicionamento e tempos controlados quando necessário. A renderização e as regras foram verificadas; a arte de documentos e a dublagem continuam provisórias conforme o pedido. Nenhum ZIP/build foi produzido.

Decisões: descoberta depende das pistas, leitura obrigatória antes de concluir a biblioteca para evitar bloqueio posterior; controle do carro preservado; rastros por pontos editáveis sem navegação pesada; partículas em cache; contornos separados dos materiais originais; migração apenas dos saves antigos avançados; fita 1 preservada. Não há novo personagem, perseguição, ataque ou dano pelo apito.
