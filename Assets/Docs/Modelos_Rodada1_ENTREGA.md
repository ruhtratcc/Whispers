# Rodada 1 — substituição e melhoria de modelos

Entrega de30/10/2026. A Rodada1 está aplicada; a Rodada2 aguarda os arquivos do Sketchfab e não foi executada. Seis trocas visuais: duas cadeiras de visitante, lixeira, luminária e carcaça do relógio no escritório; banco do vestíbulo próximo da cantina na escola. Mantidos mostrador/ponteiros originais. RoadJourney permanece o carro atual porque não existe veículo PH com interior adequado no catálogo consultado.

## Auditoria completa e decisões

1686 entradas agrupadas por asset/malha+cena+decisão; 12778 instâncias de Renderer de modelos. Um batch tem malha própria e não equivale a um objeto artístico único. Inventário geral do projeto: 13512 arquivos de malha/modelo, incluindo arquivados e sem uso nas três cenas. Arquitetura, inspeção e objetos da história foram classificados e mantidos.

Origem das entradas: IA/procedural 1614, Poly Haven 56, Poly Pizza 3, outro/Kenney 8, desconhecida 5. “IA” neste relatório identifica geometria gerada pelos builders anteriores, não prova de geração por Meshy. Decisões da auditoria: A 12; B 60; C 1518; D 96. A classifica renderers/partes do mesmo prop; não significa12 modelos artísticos diferentes trocados. O registro efetivamente aplicado tem6 alvos lógicos.

As5 origens não confirmadas são prefabs antigos de documentos/fotos e a fita antiga. Foram preservadas como itens específicos; não usadas como fonte de downloads novos. O crédito histórico não substitui prova de origem. Tabela por cena e CSV por instância em Substituicao_Modelos.md e Substituicao_Modelos_Instancias.csv.

Poly Pizza em uso auditado:3 entradas de cena/famílias (apito, chave e celular),0 substituídas nesta rodada,3 mantidas. Apito Poly by Google CC-BY3.0 continua creditado; chave/celular Quaternius CC0 documentados. Criança rejeitada: zero arquivos conhecidos sob Assets, não reintroduzida; retirada da atribuição ativa, história da rejeição mantida no registro. Ventilador CreativeTrio foi substituído na seleção anterior, não agora.

## Materiais, geometria e orçamento

Novos Poly Haven usados: Green Chair01/Kirill Sannikov; Painted Wooden Bench/Kirill Sannikov; Desk Lamp Arm01/Kuutti Siitonen (modelagem/textura), Yann Kervran (rig). CC0, mapas1K, originais/hashes preservados. MidCentury Lounge Chair foi baixada para avaliar, rejeitada por proporção e movida para _Substituidos/Avaliados_nao_usados; não está nas cenas. Quatro downloads avaliados, três modelos novos efetivamente usados. Reuso local: Wall Clock e Metal Trash Can. Cadeiras escolares, carteiras, livros, luminárias e plantas PH existentes foram mantidos.

Materiais URP/Lit compartilhados, emissão desligada, instancing habilitado. ARM convertido por ferramenta Editor: R←metallic; A←255−roughness; AO no verde. Normais GL mantidas sem inverter; conversão DX tem teste de inversão só do verde. LODs da luminária detalhados só perto. Cadeira do PC, fontes/Canvas/RT, FOV49, sistema de inspeção e pontos de pegar não foram alterados. Volumes finos de livros nas estantes não receberam malha espessa que mudasse proporções/custo; os livros PH da escola já são mantidos.

### Inventário de cena — triângulos de renderers habilitados

Inclui LOD0+LOD1 serializados habilitados no Editor; não é a contagem de um frame renderizado. Materiais únicos incluem referências da cena, inclusive componentes preservados. Fontes/dados comparáveis antes/depois na mesma captura nativa.

| Cena | Triângulos antes → depois | Materiais únicos antes → depois | Renderers ativos antes → depois |
|---|---:|---:|---:|
| Chapter1Office | 122,612 → 169,864 | 47 → 54 | 1964 → 1840 |
| RoadJourney | 188,898 → 188,898 | 73 → 73 | 462 → 462 |
| SchoolCEEP | 14,755,840 → 14,757,033 | 300 → 301 | 7164 → 7165 |

### Contadores reais de renderização no Editor

Mesma câmera inicial, resolução907×510, FOV49; mediana de20 frames e avanço de frame comprovado. Draw calls/SetPass vêm de UnityEditor.UnityStats; o marcador genérico de profiler retornou0 e foi descartado. Triângulos vêm do contador de render do profiler. Contagem observada do Editor pode incluir passes/sombras/SceneView; não é isolamento por câmera nem desempenho de build. NÃO extrapolar FPS. As cenas com mais detalhe podem aumentar triângulos mesmo reduzindo submissões.

| Cena | Draw calls antes → depois | Triângulos de frame antes → depois | SetPass antes → depois |
|---|---:|---:|---:|
| Chapter1Office | 6,756 → 6,279 | 406,290 → 537,708 | 80 → 75 |
| RoadJourney | 332 → 332 | 377,981 → 377,981 | 43 → 43 |
| SchoolCEEP | 5,841 → 5,841 | 10,016,214 → 10,018,468 | 228 → 244 |

O carro não mudou geometricamente/materialmente. Variações de contadores na estrada, quando ocorram entre aquecimentos, não são ganho atribuído a este passe. Capturas e20 amostras brutas em outputs/ModelPass20261030/Capturas e measured-metrics.json.

## Testes e preservação

31 testes ÚNICOS PASS/0falhas finais:6 do conversor Editor;5 de escritório;11 de estrada;6 de escola;3 de captura antes/depois (mesmos IDs, não somados duas vezes). Interações reais incluem PC/emails/gaveta/apito/saída, câmera dentro do carro/rádio/celular/lanterna/porta, biblioteca com duas folhas e passagem de cápsula, fita e blackout/checkpoints. Testes temporários preservam preferências e não acessam o save pessoal. Compilação final sem erros. Não afirmar que o buffer histórico não contém diagnósticos antigos.

51 verificações de integridade PASS: todos os colliders e scripts/referências originais comparados entre snapshots; transforms/tags/layers/static flags/lightmapIndex dos renderers originais preservados; nenhum MissingScript/material ausente/shader de erro nas3 cenas. Os3 meshes ausentes serializados do escritório e35 da escola são textos TMP gerados em runtime, já presentes antes, sem regressão. Sem NavMeshData como dependência; não foi rebakeado nem foi inventado um NavMesh válido.

373 arquivos protegidos mantêm SHA256. Só mudaram2 cenas (escritório/escola) e1 receita de Editor (aceita o cubo comprovado do banco). RoadJourney, MainMenu, dados de história, voz, saves, inspeção e PC permanecem iguais. Suporte do banco comprovado em pisoY=0. Aparência foi inspecionada nas capturas selecionadas; caminhar o mapa inteiro em todos os ângulos, toda interpenetração/flutuação preexistente, animações de modelos futuros e hardware/build standalone: NÃO VERIFICADO.

Diagnósticos resolvidos nesta execução: primeiro fixture do carro referia controlador no GO errado; carry descartável da escola faltava campo exigido pela validação; importador existente usa nomes de textura planos; cenas vazias não têm SceneManagerSetup restaurável; duas estantes repetem nome; receita do banco precisava ser explicitamente reconhecida. Nenhum desses diagnósticos exigiu alterar a lógica de gameplay. A primeira poltrona foi revertida usando os IDs originais antes da alternativa final. Não reexecutar instaladores já concluídos.

## Sketchfab e Rodada2

10 pedidos agrupados,26 candidatos nos pedidos, verificados por metadados públicos. Carro tem5 candidatos. Recomendações prioritárias:

- [2008 Baird Silver Coronet Taxicab](https://sketchfab.com/3d-models/2008-baird-silver-coronet-taxicab-d4c035d5857f4b49baf2ee27f7051a16)
- [Game Ready - Rusted Locker](https://sketchfab.com/3d-models/game-ready-rusted-locker-c3a4c76c7d22483eb91aa6fd949cf157)
- [Low poly Drinking Fountain](https://sketchfab.com/3d-models/low-poly-drinking-fountain-383b49d7f1db4f6bbf22ac97ea72cb6d)
- [Phone](https://sketchfab.com/3d-models/phone-eb42c07e79764c74aa5377e092f783ce)
- [Modern Generic Smartphone](https://sketchfab.com/3d-models/modern-generic-smartphone-f4d11e48cc52479c802ec2cf0d629f68)

O carro Baird2008 é recomendação condicional, não modelo aprovado/perfeito:85.369tri/6materiais e interior declarado; excede a preferência de30mil e exige inspeção/LOD/remoção de táxi. Sedan GLB18.247tri tem44materiais/111texturas; kitGerhald20.954tri/2materiais não é carro completo. Interior completo de cada arquivo e marcas remanescentes: NÃO VERIFICADO sem download. Nada foi baixado do Sketchfab, nem a Rodada2 foi executada. Pastas de entrada criadas e configurador/checklist preparados; glTF/GLB aguardam conversão estática posterior.

## Arquivos e backups

- Assets/Docs/Substituicao_Modelos.md — auditoria por cena/decisão e origens.
- Assets/Docs/Substituicao_Modelos_Instancias.csv —12778 instâncias.
- Assets/Docs/Inventario_Modelos_Projeto.csv —13512 arquivos de malha/modelo e uso nas3 cenas.
- Assets/Docs/Sketchfab_Pedidos.md — candidatos/licenças/formatos/pastas e escolhas condicionais.
- Assets/Docs/Modelos_Como_Testar.md e Modelos_Rodada2_Checklist.md — revisão no Play e preparação de importação.
- Assets/SchoolCEEP/Editor/ModelReplacementTools.cs — único utilitário reutilizável de empacotamento/URP/preparação de entrada.
- Assets/SchoolCEEP/Editor/SchoolBatchVisualReplacement.cs — uma receita de cubo adicionada para remoção comprovada do visual antigo do banco.
- Assets/HorrorGameplay/Tests/EditMode/ModelReplacementToolsTests.cs — pixels/canais/normais/caminhos/preservação.
- Assets/HorrorGameplay/Tests/PlayMode/ModelReplacementCaptureTests.cs — captura explícita em perfis descartáveis.
- Assets/ThirdParty/ModelPass20261030/ — originais, texturas1K, meshes, materiais e prefabs novos.
- Assets/ThirdParty/LICENCAS.txt e Assets/CREDITS.md — autoria, uso e retirada de fonte rejeitada sem uso.
- Assets/_Substituidos/ModelPass20261030/Backup/ — cópias de cenas/prefabs; extensão.bak evita importação como cópia jogável/GUID duplicado; metadados originais em.original.txt.
- Assets/_Substituidos/ModelPass20261030/Visuais/ — prefabs dos visuais anteriores. Geometria interna do Unity não é arquivo movível e meshes compartilhados continuam usados pela cadeira do PC.
- Assets/_Substituidos/ModelPass20261030/Avaliados_nao_usados/ — modelo de poltrona avaliado e arquivado, com originais preservados.

Backups brutos anteriores e hashes em work/ModelPass20261030/Backup e backup-manifest.json. Nenhum arquivo original apagado, nenhum ZIP. Para restaurar: usar a cópia anterior da cena com seu metadado original, em sessão fora de Play; não importar.bak como nova cena. Não rodar geradores antigos de mapa/escritório sobre esta entrega sem rever a operação.
