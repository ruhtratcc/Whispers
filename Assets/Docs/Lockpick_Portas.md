# Arrombamentos opcionais — auditoria de portas

SchoolCEEP, 01/11/2026. Nenhum capítulo depende de grampo. A gaveta original da secretaria continua livre e contém a chave da biblioteca. Nenhuma porta existente recebeu tranca nova: 129 portas auditadas, zero mudanças de chave/tranca/pares, zero IDs duplicados. Os novos arquivos são objetos de mobiliário opcionais, sem abrir salas que estavam bloqueadas.

| ID persistente | Cena / local | Capítulo | Dificuldade | Recompensa | Motivo | Risco / cuidado |
|---|---|---|---|---|---|---|
| `bd.lockpick.lock.0` | SchoolCEEP / Gaveta de treino — secretaria | 3–4 | Fácil / 1 pino | Pilha 75 s; proposta 1 aguardando aprovação | Treino sem custo obrigatório | Bandeja acessível após abrir; conferir visualmente no Play |
| `bd.lockpick.lock.1` | SchoolCEEP / Arquivo lateral — secretaria | 3–4 | Fácil / 1 pino | 1 grampo reserva | Exploração opcional, fora do trajeto | Bandeja acessível após abrir; conferir visualmente no Play |
| `bd.lockpick.lock.2` | SchoolCEEP / Arquivo — desenho técnico | 4–4 | Média / 2 pinos | Pilha 75 s; proposta 2 aguardando aprovação | Exploração opcional, fora do trajeto | Bandeja acessível após abrir; conferir visualmente no Play |
| `bd.lockpick.lock.3` | SchoolCEEP / Arquivo — refeitório | 4–4 | Média / 2 pinos | 1 grampo reserva | Exploração opcional, fora do trajeto | Bandeja acessível após abrir; conferir visualmente no Play |
| `bd.lockpick.lock.4` | SchoolCEEP / Arquivo — informática A | 4–4 | Média / 2 pinos | Pilha 75 s; proposta 3 aguardando aprovação | Exploração opcional, fora do trajeto | Bandeja acessível após abrir; conferir visualmente no Play |
| `bd.lockpick.lock.5` | SchoolCEEP / Arquivo — diretoria | 4–4 | Média / 2 pinos | Pilha 120 s + 2 grampos; propostas 4/5 aguardando aprovação | Exploração opcional, fora do trajeto | Bandeja acessível após abrir; conferir visualmente no Play |

Capítulo 3: dois arquivos; capítulo 4: quatro novos, total máximo seis disponíveis; capítulos 5/6 sem arrombamentos novos. A diretoria é representada pelo arquivo em seu acesso administrativo, sem tornar uma sala bloqueada parte do percurso. A maior recompensa de recursos está ali; a maior recompensa narrativa permanece apenas proposta, sem publicação automática.

## Portas principais preservadas

- `school.door.0.24` — BeforeDemolitionSchoolFlow.secretaryDoor.
- `school.door.0.27` — BeforeDemolitionSchoolFlow.administrationDoor.
- `school.door.0.46` — BeforeDemolitionSchoolFlow.libraryDoor.
- `bd.door.stairs` — BeforeDemolitionFourStudentsFlow.stairDoor.
- `school.door.1.2` — BeforeDemolitionTwoVoicesFlow.sideDoor.
- `school.door.1.9` — BeforeDemolitionTwoVoicesFlow.mirrorDoor.

Salas dos quatro alunos: `school.door.0.84`, `school.door.0.17`, `school.door.0.40`, `school.door.0.57`. Todas mantêm seus requisitos.

Outras portas com chaves preservadas:

- `school.door.0.0` — `school_key`; par `school.door.0.70`.
- `school.door.0.70` — `school_key`; par `school.door.0.0`.
- `school.door.1.9` — `room_113_key`; par `sem par`.
- `school.door.0.9` — `school_key`; par `school.door.0.73`.
- `school.door.0.73` — `school_key`; par `school.door.0.9`.
- `school.door.0.40` — `school_key`; par `sem par`.
- `school.door.0.46` — `bd.library.key`; par `school.door.0.47`.
- `school.door.0.47` — `bd.library.key`; par `school.door.0.46`.
- `simplified.gate.iron` — `iron_key`; par `simplified.gate.iron.right`.
- `simplified.gate.iron.right` — `iron_key`; par `simplified.gate.iron`.
- `simplified.gate.stairs` — `room_113_key`; par `sem par`.
- `bd.door.stairs` — `bd.stairs.key`; par `sem par`.

## Evidência e candidatos descartados

`final-audit.json` compara cada ID original, chave, tranca e par. `preflight.json` e a instalação validaram volume do móvel e espaço para o jogador com Physics.CheckBox/CheckCapsule. O teste PhysicalInteractionsReachTheTapeAndOnlyItsFullPlaybackOpensTheExit completou a progressão principal do capítulo 3 sem coletar grampos. Os testes de saves cobrem ambos os finais e todos os capítulos; leitura do StoryBook confirma ausência de requisitos lockpick.

As portas de salas secundárias com SchoolAreaRestriction, portas pareadas e qualquer porta com chave foram descartadas: reabri-las mudaria bloqueios de mapa/progressão. Os arquivos novos reutilizam a malha local de Filing Cabinet, separando só a gaveta superior: 554 + 98 = 652 triângulos, sem duplicar a frente original.

Inicial: `bd.lockpick.helena-pin`, dentro da gaveta original, dois grampos. Reserva livre `bd.lockpick.loose-reserve`, mesma gaveta, somente após obter o primeiro grampo. Ambos exigem a gaveta aberta. Gavetas opcionais usam `bd.lockpick.drawer.0..5`; recompensas e coletas possuem IDs próprios.

As cinco anotações são assets PROPOSTA — APROVAR e NÃO estão ligadas ao jogo. Assim, o treino entrega a pilha agora; sua anotação será conectada somente depois da aprovação do texto, como solicitado.
