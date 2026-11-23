# Capítulo 4 — O que foi deixado para trás

O pedido “continue fazendo o jogo” autorizou este capítulo. A entrega segue o documento de terror psicológico: nenhuma presença persegue, ataca ou bloqueia João. O capítulo seguinte aguarda aprovação.

## O que jogar

Depois dos cartões do Capítulo 3, João permanece onde estava, com seus equipamentos, documentos, bateria, sanidade e estado das portas. O objetivo leva ao bloco antigo, chamado “hospício”, à esquerda no fim do corredor central.

1. Investigar o relatório no Grêmio estudantil. Ele cita João e indica a Sala de apoio aos alunos, o Depósito técnico e o Arquivo antigo.
2. Ler o verso da fotografia na Sala de apoio: “João de Claus — aluno.” A foto fica nas anotações.
3. Pegar a chave no Depósito técnico. Há um caderno familiar que recupera sanidade e pilhas de reserva.
4. Voltar pelo corredor. A figura aparece parada, longe. Some após o olhar prolongado ou ao desviar o olhar. Outra presença pode surgir mais perto, sempre fora da visão; não se aproxima em movimento. Quem continua olhando também consegue seguir a história.
5. Investigar “ELE LEMBROU DE VOCÊ.” perto do Arquivo antigo. A criança indica a porta.
6. Usar a chave na porta existente da antiga Informática B. Surgem “CAPÍTULO 4 CONCLUÍDO”, a frase “A chave estava esperando por ele.” e o título do Capítulo 5.

Portas e uma cadeira participam de eventos discretos; a porta alterada pode ser reaberta. Três luminárias têm breves eventos isolados, depois ficam apagadas. A sanidade reage uma única vez a cada revelação e conserva recuperação com lanterna e âncoras. GAME OVER por blackout reinicia o Capítulo 4 com o estado de entrada, preservando os capítulos anteriores.

## Como testar

No menu, escolha Continuar com um checkpoint escolar, ou jogue até terminar o Capítulo 3. Um checkpoint antigo com o Capítulo 3 concluído também mostra os cartões e continua para o 4. Use E para pistas/portas, F para lanterna e I para objetivo/anotações. Leia o relatório, encontre a fotografia e a chave, depois volte pelo corredor. Experimente olhar fixamente e desviar da presença. Pause durante um evento e confirme que o progresso fica suspenso.

Salve no meio do capítulo, continue e confira que as pistas coletadas permanecem nas anotações. No blackout, Tentar novamente deve voltar ao início deste capítulo, mantendo os documentos do Capítulo 3. A conclusão termina nos cartões; não há conteúdo jogável do Capítulo 5 nesta entrega.

## Integração e limites

A escola recebeu um grupo próprio de conteúdo e um StoryBook escolar próprio. Menu, prólogo, sedã e prefab compartilhado preservados. Não foi reconstruído o mapa. Cadeira e caixa reutilizam os modelos CC0 já aprovados, com meshes/LODs compartilhados; nenhum download novo.

Código canônico em `work/ChapterFour`, com as pequenas extensões do diretor em `work/RoadChapter/NarrativeDirector.cs` e da sanidade em `work/ChapterThree/Runtime/SchoolSanityController.cs`. Instalado nativamente no projeto Unity existente. IDs anteriores e SaveData v2 preservados. O ID legado `left-behind.chase-started` significa somente a segunda presença imóvel.

A duração e a intensidade ainda precisam de uma sessão humana. Os testes automatizados usam posições apoiadas para as pistas; isso não prova caminhada contínua por todo o campus. Não foi feita build de PC nem medição de FPS. A silhueta e os documentos são arte simples, ainda passível de polimento.

Evidências de instalação, testes, integridade e prévias ficam nesta pasta. O grupo novo pode ser auditado sem reinstalar ou duplicar objetos; o builder mantém o capítulo em futuras reconstruções explicitamente solicitadas.

## Validação final — 08/10/2026

Compilação nativa concluída, sem erros. **28 testes PlayMode passaram, zero falhas:** 6 de transição/baseline compartilhados, 3 do Capítulo 4 na escola real, 3 de regressão do Capítulo 3, 8 de sanidade, 7 de narrativa e 1 de sequência completa do Capítulo 3.

Os testes do Capítulo 4 verificam pistas pelo raycast real, aparições imóveis e ausência, pausa, mensagem/porta, save/load e blackout/retry. A transição real do desenho final do Capítulo 3 conserva posição/equipamentos. Há caminhada contínua com W por cerca de oito metros durante a volta pelo corredor, com cápsula livre e piso apoiado; o restante das pistas usa posições apoiadas. Rodadas iniciais de diagnóstico estão separadas dos relatórios finais.

`final-scene.json` confirma escola salva/limpa, fora de Play, sanidade inicial 100, nenhuma luminária elétrica ligada, pistas/aparições iniciais ocultas, uma câmera, zero scripts ausentes, inimigos ou IDs duplicados. A arquitetura existente mantém a assinatura física anterior. Os oito colliders do grupo novo correspondem às três pistas, ao volume de retorno, à âncora, às duas pilhas e ao apoio físico da caixa; entidades têm zero colliders.

`protected-after.json` confirma SHA256 idêntico de MainMenu, Chapter1Office, RoadJourney e GameplayRoot. Checkpoints e preferências anteriores foram restaurados e o guard foi encerrado. Prévias nativas `presenca.png`, `mensagem.png`, `relatorio.png` e `chave.png` foram inspecionadas; a câmera da chave usa uma posição onde a cápsula do jogador cabe e o raycast alcança o objeto.
