# Whispers of the Past — Capítulo 3: A Escola Abandonada

O Capítulo 3 está integrado à escola existente. O fim da viagem agora carrega SchoolCEEP, mantendo os equipamentos, a bateria e o progresso anterior. A conclusão deste capítulo apresenta “CAPÍTULO 3 CONCLUÍDO”, a frase “A escola não está tão vazia quanto parece.” e o título do Capítulo 4. O Capítulo 4 continua indisponível, aguardando aprovação do usuário.

## O que foi entregue

- Entrada que fecha e tranca, registros na Secretaria, foto da turma com um rosto apagado, inscrição “JOÃO, VOCÊ VOLTOU.” que desaparece, aparição infantil imóvel e desenho “NÃO SUBA.”. A sequência existente foi preservada e recebeu objetivos mais específicos.
- Sanidade persistente, desgaste no escuro, reação a pistas e aparições, recuperação com a lanterna e duas âncoras de memória. Sem barra grande: tensão nas bordas, interferência breve, batimentos discretos e leve oscilação da lanterna quando a sanidade está baixa.
- Blackout por sanidade zero, legenda “Você nunca deveria ter voltado.”, GAME OVER e Tentar novamente. O retorno usa uma cópia do início do capítulo, preservando equipamento e bateria da chegada e removendo as pistas e alterações posteriores.
- Escola sem iluminação elétrica permanente: 156 luminárias desligadas, luar, poste externo e lanterna. Falhas sobrenaturais acendem uma luminária por menos de um segundo e retornam à escuridão. Pausa, leitura e sequências suspendem a exposição ao escuro.
- Abandono em 90 cômodos acessíveis, com mofo, reboco, rachaduras, sujeira, papéis, folhas, teias, mochilas, cadernos, relógios parados, desenhos desbotados, vidro e até seis janelas com tábuas. O catálogo tem 91 cômodos; Automotiva permanece inacessível.
- Eventos espaçados com Foley 3D existente, intervalos de silêncio e pressão de sanidade. Uma cadeira muda de posição fora da visão depois da foto; sua posição é salva e restaurada silenciosamente.
- Quatro reservas de pilhas, poeira no feixe e objetivo consultável junto das anotações com I. O menu de pausa e as opções existentes são reutilizados.

Não há perseguição, ataque, combate, pontuação ou collider na criança. Os diálogos permanecem somente por legendas.

## Como jogar e testar

1. No Unity, a cena SchoolCEEP está salva e aberta, fora de Play. Pressione Play para testar a escola diretamente. Pelo menu, Novo jogo percorre o prólogo e a viagem antes da escola.
2. Entre, volte à porta e tente abri-la. Siga o objetivo até a Secretaria; investigue os registros e a fotografia com E ou clique.
3. Vá à Sala 02 e olhe para o quadro. Olhe para o corredor e volte ao quadro. Siga para a Sala 03, aproxime-se da criança e investigue o desenho.
4. O apagão narrativo conclui o capítulo. Este apagão é distinto da derrota por sanidade: a derrota oferece Tentar novamente, enquanto a conclusão mostra o próximo título.
5. Para experimentar a sanidade, desligue a lanterna com F depois de entrar e permaneça no escuro. Há 20 segundos de tolerância; depois, a perda é gradual. Ligue a lanterna ou aproxime-se da foto familiar na Secretaria ou do relógio no hall para recuperar.
6. Use I para consultar objetivo, documentos e equipamentos. Esc pausa. Os controles existentes de movimento, corrida, agachamento e salto permanecem.

Os checkpoints antigos da escola que não possuem a cópia do início conservam o equipamento e a bateria disponíveis naquele checkpoint. A bateria original de chegada não pode ser reconstruída retroativamente.

## Validação

**45 testes nativos aprovados, zero falhas na rodada final:**

- 8 testes de sanidade, blackout, persistência, pausa e apresentação da lanterna.
- 3 testes na escola real: chegada com equipamento da estrada, anotações/objetivo, blackout e clique no botão de reinício; transição real RoadJourney–SchoolCEEP; falha elétrica e pausa.
- 1 teste da sequência completa na escola real, com raycasts para pistas, portas, quadro, criança, desenho e conclusão. Este teste usa posições apoiadas e deslocamentos controlados; não comprova uma caminhada contínua por toda a escola.
- 6 testes do fluxo narrativo escolar.
- 23 testes de catálogo, progressão e compatibilidade dos saves.
- 2 testes da atmosfera e 2 testes da viagem, incluindo caminhada contínua até o portão, coleta separada da lanterna e saída pela porta.

Os arquivos de checkpoint e as preferências foram restaurados ao terminar. MainMenu, Chapter1Office e GameplayRoot mantiveram seus SHA256. RoadJourney recebeu a ligação para o capítulo seguinte. O mapa da escola preservou a assinatura dos colliders, posições, escalas e portas existentes; a decoração adicionou zero colliders. Apenas os novos objetos de interação do capítulo possuem pequenos colliders próprios.

A decoração acrescenta 7.284 triângulos, agrupados por material e setor, e 13 texturas de 128×128. Não foi feita build de PC nem medição de FPS. Ainda cabem uma sessão humana completa para ajustar duração/tensão e uma etapa posterior de arte mais realista; os objetos e texturas novos usam arte nativa simples.

Relatórios: installation.json, final-scene.json, protected-after.json, sanity-tests.json, scene-tests.json, opening-scene-tests.json, opening-tests.json, narrative-save-tests.json, atmosphere-tests.json e road-regression-tests.json. As rodadas iniciais de diagnóstico foram preservadas separadamente; não são a rodada final.

## Organização

Runtime em Assets/HorrorGameplay/Scripts: SchoolSanityController, SchoolSanityView, SchoolMemoryAnchor, SchoolAnchorInspection, SchoolObjectiveJournal, SchoolFlashlightDust, SchoolPerceptionEvents e SchoolHorrorAtmosphere. O fluxo de pistas continua em SchoolOpeningFlow.

Instalação nativa e decoração: Assets/SchoolCEEP/Editor/SchoolChapterThreePass.cs e SchoolAbandonmentPass.cs. Os grupos próprios “Escola — capítulo 3 e sanidade” e “Escola — abandono do capítulo 3” podem ser substituídos pelos passes sem reconstruir a arquitetura. O perfil visual é uma cópia com seus próprios componentes; os materiais de luminárias apagadas também pertencem à escola.

As reconstruções explícitas posteriores preservam o capítulo quando seu StoryBook existe. Os IDs antigos de narrativa são mantidos para compatibilidade; IDs com nomes históricos de perseguição não representam mecânicas implementadas.

Prévias: entry.png, corridor.png, documents.png, blackboard.png e ghost.png. São renders da cena pelo Unity com a lanterna equipada; os estados narrativos foram apenas ativados temporariamente para inspeção e restaurados antes de salvar.
