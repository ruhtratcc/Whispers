# Refino de cena — 05/11/2026

Pedido atual: manter o reflexo dentro do espelho. Também concluir os ajustes em andamento de espelho intacto, luz nas apresentações, árvores, biblioteca, pegadas, grades da rota para Automotiva e alcance do apito.

## Alterações
- Reflexo reposicionado atrás do vidro e recortado pelo stencil da própria malha do vidro. Máscara invisível, sem collider. Corpo/cabelo/barba/sorriso usam o mesmo recorte; não projetam sombras no ambiente.
- Luz do reflexo sincronizada com a lanterna, em rendering layer 128 exclusiva, sem iluminar os objetos reais da sala. URP rendering layers já estavam habilitadas; nenhuma opção global alterada.
- O espelho e o reboco que cobre o vão ficam presentes antes de seu capítulo. Saves que já registraram a recusa continuam com a abertura revelada.
- Apresentações na escola mantêm um feixe temporário, mesmo com mão oculta ou carga zerada; bateria, equipamento escolhido e posição do interruptor não são sobrescritos pelo feixe. O campo legado do save também preserva o interruptor real.
- Restavam 12 árvores primitivas fundidas às malhas de terreno e madeira. Removidas 9.216 faces das copas e 960 dos troncos; criadas malhas substitutas e atualizadas as referências do corredor. Reutilizadas árvores Poly Haven locais nas 12 posições. Mantidas as 14 árvores Poly Haven anteriores e as da estrada.
- Biblioteca: mesmos 180 livros nas seis estantes, agrupados com inclinações e alturas variadas; 36 deitados/empilhados. Outros 15 livros, inclusive o que flutuava, reposicionados ao piso.
- 39 marcas de pegadas receberam deslocamentos e rotações variados; texturas, evidências e respostas ao apito preservadas.
- As 24 pilhas usam alcance visual de 18 m (antes 24 m). Destaques e trilhas usam faixa vertical configurável de 2,5 m a partir dos pés do jogador, impedindo a revelação testada do outro andar.
- Correção do local das grades: as duas da quadra foram restauradas com seus estados originais. Foram ocultados renderers e colliders das 14 peças do cercamento da Automotiva, sem excluir raízes, IDs, scripts ou limites naturais.
- O destaque do espelho apontava para uma película antiga fora do grupo do vidro. Agora aponta somente para o vidro real, mantendo ID e condições.

## Arquivos principais
- Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionMirrorFlow.cs — presença inicial do espelho e estado de apresentação.
- Assets/HorrorGameplay/Scripts/Narrative/MirrorReflectionMask.cs — recorte, materiais temporários e luz exclusiva do reflexo.
- Assets/HorrorGameplay/Shaders/MirrorReflectionMask.shader — stencil da malha real do vidro (bit 8, parte da UserMask da URP instalada).
- Assets/HorrorGameplay/Shaders/MirrorReflectionActor.shader — ForwardLit da URP com stencil explícito dentro do passe.
- Assets/HorrorGameplay/Scripts/Equipment/EquipmentController.cs — estado de iluminação nas apresentações.
- Assets/HorrorGameplay/Scripts/Player/FlashlightController.cs — luz temporária sem reativar o modelo da mão.
- Assets/HorrorGameplay/Scripts/Systems/SaveSystem.cs — preservar interruptor real durante o feixe temporário.
- WhistleConfig, WhistleSense, WhistleHighlightable, WhistleTrailRoute e WhistleConfig.asset — faixa vertical e filtro dos destaques.
- Assets/SchoolCEEP/Scenes/SchoolCEEP.unity — ajustes de posicionamento e vínculos.
- Assets/SchoolCEEP/Meshes/SceneRefinements20261105/ — malhas limpas, originais mantidos.
- Assets/HorrorGameplay/Data/SceneRefinements20261105/MirrorSurfaceMask.mat — referência de shader para cena/build.
- Assets/ThirdParty/LICENCAS.txt — origem dos shaders e reuso de assets.

Backups: Assets/_Substituidos/SceneRefinements20261105/. Nenhum download, gravação nova, TTS, geração paga ou ZIP.

## Provas e limites
**42 testes únicos aprovados, zero falhas nos resultados finais:** seis Editor, dois nativos das novas regras, um recorte por GPU, 15 equipamento, cinco destaques do apito, dois respostas das crianças, um iluminação, nove espelho e um final principal na escola até o email final. Resultado consolidado: `testes-finais.json`.

- GPU: 1.944 pixels vermelhos dentro do vidro e **zero fora**, usando uma figura de teste propositalmente maior que o espelho. Vidro oculto também oculta o reflexo.
- Escola real: diferença média de pixels de 0,02271153 dentro do espelho entre reflexo visível e referência sem a figura. Testado com a iluminação normal da lanterna, mão escondida, bateria zerada e interruptor real desligado; comprovada a preservação desse estado no save descartável. Rendering layer 128 exclusiva verificada por teste.
- Cena reaberta: 180 IDs preservados, zero scripts ausentes, zero erros nos shaders, 24 alcances de pilhas de 18 m e nenhum feixe temporário serializado. `work/SceneRefinements20261105/audit-final.json`.
- A revisão visual final revelou livros atravessando tábuas: corrigidos os 180 livros usando raycasts diretamente na malha da estante, incluindo o teto livre entre tábuas. Erro máximo de apoio: 0,0000000675 m; penetração máxima na tábua acima: zero. Essa última alteração só mudou poses/escala dos livros. Cena novamente reaberta e conferida; IDs e contagens intactos. Prova: `work/SceneRefinements20261105/LibrarySupport/installed.json`.
- Compilação final concluída sem erros. Testes usam saves temporários; nenhum arquivo do save pessoal foi lido ou escrito.
- Unity restaurada em SchoolCEEP, sem alterações pendentes, fora do Play, sem compilação e com `TestSaveDirectory=null`. Os dois guards foram fechados: `workspace.json.restored` e `LibrarySupport/workspace.json.restored`.

As falhas diagnósticas anteriores foram mantidas em `work/SceneRefinements20261105`; não contam como aprovação. O primeiro UsePass deixava o reflexo escapar; o passe explícito resolveu. A iluminação original era bloqueada pelo vidro/parede; a luz exclusiva do reflexo evita alterar as sombras do ambiente.

Três testes antigos precisaram acompanhar regras já existentes: o botão do mouse agora dá um único apito em vez de sustentar; o celular automático pode interromper a exploração, então os testes isolados do apito fecham/desativam esse modal; o teste do som confere a gravação vinculada em vez do nome antigo do arquivo. Não foram mudados o celular nem o sistema de reprodução do apito para acomodar esses testes.

## Capturas
- `mirror-intact-cinematic-light.png`: escola real, reflexo contido no vidro e luz normal da apresentação.
- `mirror-gpu-mask.png`: prova isolada do recorte, figura vermelha maior que o vidro.
- `biblioteca-estante-diagnostico.png`: malha e livros reais de uma estante, isolados das paredes para inspecionar o apoio, com luz temporária de diagnóstico. Não representa a iluminação final do gameplay. Nenhuma dessas condições de diagnóstico foi salva na cena.
- As capturas `mirror-diagnostic-*` e `biblioteca-diagnostico.png` são tentativas anteriores, não o resultado final.

## Decisões e limites
- O recorte usa a malha do vidro, não um retângulo aproximado. Não foi criada outra câmera de espelho nem outra RenderTexture.
- O reflexo se apaga junto com o vidro quebrado. Um save em que João já recusou mantém o vão aberto; não se altera o histórico da escolha.
- Foram preservados efeitos, falas, progressão, configurações da câmera/FOV 49, menu e escritório.
- Nenhum novo download. Os modelos e texturas continuam com os créditos locais; a adaptação do ForwardLit conserva a licença do pacote Unity.
- **NÃO VERIFICADO:** executável/standalone, campanha inteira percorrida manualmente, avaliação humana de áudio e todos os ângulos possíveis do espelho. A conferência visual da biblioteca usou a captura diagnóstica descrita acima.
