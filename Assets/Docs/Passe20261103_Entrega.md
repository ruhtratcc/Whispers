# Passe de polimento de Whispers of the Past

Entrega de 03/11/2026 no projeto `C:/Users/ruhtra/My project`.

Implementação e fechamento técnico deste passe concluídos. Há acabamento de produção identificado abaixo, principalmente vozes humanas, demos e alguns placeholders. Não se afirma que o mapa ou a campanha estejam livres de todo defeito. Save pessoal não foi lido nem alterado. FOV 49 preservado. Nenhum ZIP ou executável novo foi criado.

## Resultado

- Automotiva inteira e salas laterais preservadas fisicamente, sem uso na progressão, com acessos fechados e barreiras de troncos. Caderno da Marta e referências transferidos para a sala de artes adaptada da 03B, mantendo IDs.
- Portão principal fixo aberto, sem prompt; duas junções laterais fechadas, passagem central de 6,43 m mantida. Grade superior usa a chave e o ID existentes; continuação antiga ao teto arquivada.
- Escada oeste deslocada 0,65 m; faixa lateral ampliada 0,45 m; patamar de 1,151 m para 1,801 m. Três travessas que prendiam a passagem foram desativadas após falha no teste físico. Subida, descida e passagem transversal aprovadas depois da correção.
- Estantes retiradas das mesas na biblioteca, livros reposicionados e mesa funcional da secretaria substituída por modelo Poly Haven local. Gaveta e conteúdo conservaram coordenadas e IDs.
- Pilhas de 16 para 24. Stamina de 7,2 para 9 s nas três cenas. Grampo com alcance de coleta ampliado e brilho discreto; seleção respeita oclusão.
- Árvores Poly Haven, variações de tronco e bloqueios naturais no acesso à quadra. Automotiva e quadra não são necessárias à história.
- Email minimalista, leitura cronológica com 2,5 s visíveis, reação uma vez por email; armário real entreaberto após o email 7 e pose de referência interna para fotografia.
- Motorista e volante à esquerda. Celular ancorado e sem física solta; vibração só no modelo. Quatro controles no rádio; volume mínimo narrativo 18%. Áudio continua no carro depois da saída, atenua com distância e só termina ao cruzar o plano de entrada da escola.
- Mensagens necessárias à progressão e mensagens por zona, recebimento/leitura separados e recuperação de falha de entrega. Interfone só após reunir os quatro objetos, ouvir Bia, ler a orientação e chegar à área central. Chave liberada na ordem correta.
- Apito de um clique, sem segurar: onda 1,6 s, destaque 10 s, cooldown 7 s. Espelho não reage ao apito. Objetivos em lista, caixas por subitem e visibilidade por estado.
- Ratos sonoros em 3D, ruídos distantes, luzes com limite de frequência e redução de efeitos. Vozes e ruídos respeitam prioridade e cooldown.
- Finais “Ficar no lugar deles” e “Não confio em você”, escolha sem cronômetro e susto forte somente após recusar. Devolução do apito, telefonema, amanhecer, TV, resposta ao email e créditos; reinício anterior à escolha e validação de saves cobertos por testes.

## Correções finais de apresentação

A captura sem monitor vinha da câmera do teste, com recorte próximo padrão de 30 cm: o CRT está a aproximadamente 22,7 cm. A câmera nativa usa 4 cm. O teste foi alinhado ao valor nativo e passou a medir posição, rotação, enquadramento e recorte; não se alterou a nitidez do desktop.

No prefab de apresentação do epílogo, a câmera da janela foi afastada da persiana. A caixa provisória da TV foi substituída por `television_02` e uma mesa já disponíveis da Poly Haven, sem download. A TV mostra uma fotografia local da escola, com recorte proporcional 4:3, e o relógio do PC de manhã marca 08:12. Responder só libera depois da leitura. Capturas reais do email aberto, escolhas e resposta foram revistas.

A fotografia do final ruim usa provisoriamente a mesma imagem da escola. Ainda falta a versão de fachada com janela superior acesa; o modelo antigo da carteira não é mais a imagem de apresentação.

## Provas

| Grupo | Passaram | Falharam | Resultado bruto |
|---|---:|---:|---|
| Editor e dados | 350 | 0 | [tests-editor-closeout-final.json](tests-editor-closeout-final.json) |
| Espelho e epílogo | 9 | 0 | [tests-mirror-epilogue-closeout-final.json](tests-mirror-epilogue-closeout-final.json) |
| Percurso da escada | 1 | 0 | [tests-map-final.json](tests-map-final.json) |
| Estrada e rádio | 2 | 0 | [tests-road-final.json](tests-road-final.json) |
| Email celular objetivos | 3 | 0 | [tests-progress-final.json](tests-progress-final.json) |
| Interfone e chave | 1 | 0 | [tests-intercom-first.json](tests-intercom-first.json) |
| Apito e integração | 23 | 0 | [tests-whistle-final.json](tests-whistle-final.json) |

**389 testes únicos aprovados, zero falhas nos resultados finais consolidados.** Compilação concluída com zero erros. Execuções iniciais com falhas foram preservadas: colisão da escada, expectativas antigas de apito/textos e erro temporário no diagnóstico do teste de câmera foram corrigidos; não foram contados como aprovação.

O teste da estrada reconstruiu vinte execuções do movimento, conferiu estabilidade do celular, ganho por distância e corte somente depois de cruzar a entrada. Isso não equivale a vinte campanhas humanas completas.

Auditoria nativa das três cenas: zero scripts ausentes, IDs persistentes duplicados, materiais de erro ou ocorrências de CEEP nos campos de texto referenciados examinados; zero rigidbodies dinâmicos. Prefab do amanhecer: zero scripts ausentes/material inválido e nenhuma cópia de GameSession. Campos internos como SchoolCEEP e arquivos históricos foram conservados. A substituição inicial dos dados alterou 13 campos CEEP para SEEPE; o relatório [Finais_Changelog.md](C:/Users/ruhtra/My%20project/Assets/Docs/Finais_Changelog.md) registra os textos antes/depois.

NavMesh: nenhuma malha ou agente está em uso nas cenas auditadas. Não houve bake e não se declara um NavMesh validado; o percurso foi testado com CharacterController.

A ferramenta `Tools > Whispers > Auditar objetos do mapa` produz candidatos de sobreposição/apoio, não uma prova geométrica de defeito. Antes/depois: escritório 3/5, estrada 6/6, escola 518/455. Esses números incluem LODs, grupos e apoios legítimos; não representam quantidade de bugs confirmados nem todos os candidatos foram inspecionados manualmente. As 164 amostras de borda tiveram suporte nos raios; não substituem exploração de todos os limites e saltos.

Capturas:

- [TV no final bom](morning-tv.png), [janela](morning-window.png), [email e resposta](morning-reply.png): apresentação real em Play.
- [Entrada](gate-final-inspection.png) e [mesa da secretaria](secretary-desk-final-inspection.png): inspeção com iluminação temporária, sem salvar essa luz nas cenas.
- [Auditoria nativa](final-native-audit.json), [validação do epílogo](presentation-native-final.json), [estado final do Editor](final-editor-state.json) e [resumo de testes](test-summary-closeout.json).

## Arquivos e ajustes

Cenas atualizadas: `Assets/ChapterOneOffice/Scenes/Chapter1Office.unity`, `Assets/RoadChapter/Scenes/RoadJourney.unity` e `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity`.

Sistemas principais em `Assets/HorrorGameplay/Scripts`: Narrative/EndingEpilogue, EndingPresentationConfig, BeforeDemolitionMirrorFlow, ContextBarks; Objectives/ObjectiveService; Phone/PhoneProgression; ambiente e interações integrados aos serviços existentes. O inventário completo dos arquivos preexistentes alterados está em [changed-existing-files.json](changed-existing-files.json). Novos dados, prefabs e modelos ficam em `Data/Polish20261103`, `Assets/models/Polish20261103` e `Assets/Audio/SFX/Polish20261103`. A ferramenta de auditoria fica em `Assets/SchoolCEEP/Editor/SchoolPropAudit.cs`.

- Apito: Inspector de BeforeDemolitionWhistle, duração da onda/destaque/cooldown; tutorial e demo marcados para o gesto novo.
- Ratos e luzes: SchoolAmbientConfig, intervalo 35–75 s para ratos, 95–160 s para ruídos distantes; perfis com frequência limitada a 2 Hz.
- Objetivos: criar/copiar o ScriptableObject de capítulo; preencher título/subitens/flags; ligar o catálogo ao ObjectiveService e conferir a sincronização com o diário.
- Rádio: transforms dos botões sob “Rádio — fonte no painel”; ganho e rolloff no BeforeDemolitionRoadRadio. Leitura do email: minimumReadSeconds no catálogo/controlador.
- Finais: `Assets/HorrorGameplay/Data/Polish20261103/Endings.asset` controla textos, tempos, sons e intensidades; marcadores EpilogueSeat/Window/TV/PC do MorningOffice.prefab controlam enquadramentos.

Backups originais de 407 arquivos em `Assets/_Substituidos/Passe20261103`, com extensão .backup. Peças removidas/desativadas em `Assets/_Removidos/Passe20261103`. Ajustes finais têm cópia adicional em `work/Passe20261103/ResumeBackup`. Não repetir os instaladores nem os scripts de append de dublagem/créditos.

## Assets e dublagem

Baixados anteriormente neste passe: Poly Haven tree_small_02, dead_tree_trunk_02 e vintage_cabinet_01, com licença CC0; dois efeitos de rato CC0 e sirene gravada CC0. Proveniência e hashes em Assets/ThirdParty/Polish20261103/Proveniencia.json, LICENCAS.txt e CREDITS.md. TV e mesas reutilizam assets locais.

98 entradas novas de dublagem no catálogo e no CSV/XLSX, total Mestre 1.010. O guia LEIA_PRIMEIRO foi atualizado para não apresentar as contagens de 30/10 como atuais. As dez reações antigas do rádio ficam desativadas até receberem takes do novo texto. Nenhuma voz foi sintetizada. O Word completo entregue separadamente inclui também textos atuais silenciosos e arquivo histórico sinalizado.

Detalhes de eventos, alterações de história e pendências estão em Assets/Docs/Eventos_Terror.md, Finais_Changelog.md, Demos_Para_Gravar.md e Sketchfab_Pedidos.md.

## Como conferir no Play

Use perfil descartável ou uma cópia do checkpoint, preservando o save pessoal.

1. Escritório: leia os emails em ordem, mantenha o foco no texto e confira a fresta/abertura do armário após o sétimo.
2. Estrada: teste botões do rádio, colete o celular e a lanterna, saia pela porta, afaste e volte ao carro; atravesse a entrada e confira o corte do rádio.
3. Escola: leia a mensagem de orientação, confira os objetivos e quatro objetos, use um toque no apito, volte à área central e obtenha a chave após o interfone. Percorra a escada nos dois sentidos.
4. Espelho: espere na escolha para confirmar que não há decisão automática. Teste os dois finais em cópias; no bom, devolva o apito, aguarde a manhã e responda ao email. Use o checkpoint anterior à escolha para testar o outro final.

## Pendências e limites

- Gravação das vozes humanas novas, demos finais de tutorial e escuta de mixagem em headset.
- Sons especializados de roer/arranhar/passos de rato e ruído de lâmpada ainda têm slots silenciosos identificados.
- Sapatos/mochilas finais usam placeholders; quatro candidatos de Sketchfab foram registrados, sem download. Fotografia definitiva do final ruim ainda pendente, conforme descrito acima.
- NÃO VERIFICADO: campanha inteira jogada por uma pessoa, sensação de controle/ritmo, todas as salas e todas as tentativas de atravessar os limites, executável atualizado ou PC de outra pessoa.

Unity encerrada do Play, cena Untitled limpa, nenhuma fila de gravação e override de save de teste nulo. O Editor permaneceu aberto. O comportamento em segundo plano foi restaurado ao valor original.
