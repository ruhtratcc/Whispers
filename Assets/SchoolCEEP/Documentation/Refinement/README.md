# Refinamento de Whispers of Past — 09/10/2026

O pedido atual autoriza polir as cenas existentes da escola e da chegada de carro. A história original, os sete capítulos, o menu, o escritório e a regra de terror psicológico sem perseguição continuam.

## Mudanças aplicadas

- Os dois estacionamentos perderam asfalto e 42 marcas de vagas. As áreas laterais viraram terreno, conservando apoio, acesso e o ponto de observação da janela final. O caminho de entrada continua aberto. As duas inscrições da escola na fachada foram ocultadas.
- RoadJourney usa uma cópia por translação da geometria da entrada da SchoolCEEP: portal, duas folhas do portão, quatro degraus, fachadas laterais, hall e fachada central ao fundo. Não copia atores, entidades, saves, pistas, scripts ou luminárias da escola. A fachada genérica saiu; limites laterais e posterior não têm parede visível. O sedã próprio, a câmera sentada, a viagem de 38 segundos, a lanterna e a saída manual permanecem.
- O início da escola fica imediatamente depois do mesmo portal, antes do volume que fecha a entrada. Apoio e cápsula em pé conferidos no Editor. Essa posição nova afeta somente uma entrada nova no capítulo; checkpoints salvos continuam sendo restaurados pelo sistema existente.
- 27 salas tiveram a distribuição de mobiliário alterada. Há 383 carteiras, 372 cadeiras, 56 carteiras tombadas, 135 cadeiras caídas e 82 caixas. Os 457 volumes antigos de colisão ficaram desativados; os novos colisores acompanham a orientação e o volume do mobiliário. Faixas internas de passagem, folhas das portas e acesso às pistas foram reservados.
- Dois modelos da Poly Haven foram aprovados pelo usuário antes do download: Office Notepads e Book Encyclopedia Set 01, ambos CC0. São 13 arquivos glTF/1K, 4.593.941 bytes, com tamanho, MD5 publicado e SHA256 local conferidos. Créditos em CREDITS.md e Assets/CREDITS.md. Foram preparados 30 prefabs individuais e três materiais, com LOD para objetos de volume.
- 223 folhas do refinamento usam a geometria/textura do papel aprovado. Outros 24 livros e 12 cadernos foram colocados em 12 salas, apoiados e fora dos acessos às pistas. São decoração sem collider ou script.
- A estrada e a escola recebem o tratamento de cor: saturação 50%, contraste controlado, sombras frias, luz mais quente e vinheta. O grão e as scanlines ficaram menos ruidosos. O filtro mantém os pulsos de sanidade, congela na pausa e deixa a UI fora do tratamento. Menu e prólogo ficam excluídos. Um blit e quatro amostras de cor no RenderGraph existente.
- O pedido posterior de névoa ambiental foi integrado: a poeira presa à lanterna ficou desativada e sem playOnAwake. A textura atmosférica já existente no projeto é usada em material separado, com emissores fixos em World: 27 salas, quatro pontos de circulação e três pontos na chegada. Partículas desaparecem perto da câmera, têm interseções suaves com paredes e não adicionam colliders/luzes. A névoa de distância deixa os primeiros 14 metros limpos. Detalhes em `ambiente-nevoa.md`.

## Revisão de arquitetura

4.200 amostras de alvenaria não encontraram uma parede esperada sem volume físico. As faces amostradas das paredes otimizadas e as caixas das escadas também passaram. A revisão independente examinou os 893 candidatos de bordas dos cômodos: são caixas aproximadas antigas, deslocadas das paredes atuais. Não foram fechados corredores legítimos para encaixar essas caixas.

Depois do mobiliário novo, os 422 trajetos examinados continuaram acessíveis, com zero juntas suspeitas e zero destinos inacessíveis. A auditoria histórica inteira contém cinco verificações antigas de gameplay incompatíveis com os capítulos já implementados (vínculo da estrada, referências de foco, intensidade da lanterna e portas narrativas trancadas); por isso seu campo geral `passed` permanece falso. Os resultados físicos estão descritos separadamente, sem esconder essas verificações históricas.

Essas amostras e trajetos não provam que cada centímetro do campus é perfeito. A etapa não encontrou um novo buraco de parede sustentado pela evidência atual e não adicionou paredes arbitrárias. A subida da entrada e os capítulos existentes são verificados separadamente em PlayMode.

## Fontes e restauração

Passes de cena em `work/Refinement/Editor`; conversor/download/provenance em `work/RefinementAssets`; filtro canônico em `work/SchoolOpening`. Instalados em `Assets/SchoolCEEP/Editor`, scripts/shader existentes e pastas `Assets/models`/`Assets/textures`.

Backups completos das duas cenas antes da etapa estão em `Before`. Meshes de terreno foram clonados antes de filtrar as 528 faces das vagas/estacionamento; os assets originais permanecem. O pass do mobiliário oferece restauração dos holders e colisores anteriores. Nenhum builder completo da escola foi executado. Os passes idempotentes ficam disponíveis para reaplicação deliberada; uma reconstrução futura pelo builder antigo exige reaplicar esse refinamento depois da arquitetura e dos capítulos.

## Testes e prévias

**25 testes diferentes passaram, zero falhas finais: seis EditMode do filtro e 19 PlayMode.** PlayMode: dois da estrada, três do Capítulo 3, três do 4, quatro do 5, seis do 6/Final e um da abertura escolar completa. Os dois testes da estrada foram repetidos depois da névoa ambiental; não contam duas vezes. A abertura integrada também passou depois da névoa.

Provas incluem equipamentos do prólogo, lanterna/porta manual do carro, mais de dez metros de caminhada contínua e subida dos quatro degraus; pistas e linhas de visão; transições, save/load, pausa e blackout/retry; subida e descida contínuas da escada até o terraço. Os dois diagnósticos anteriores da estrada ficam históricos: expectativas do apoio plano e do centro da cápsula foram substituídas por apoio no seu footprint real e prova de subida. Não houve alteração no motor para satisfazer o teste.

`final-SchoolCEEP.json` e `final-RoadJourney.json`: cenas salvas/limpas fora de Play, uma câmera/jogador/sessão cada, zero inimigos, scripts/meshes/materiais ausentes, IDs vazios/duplicados, erros de história ou shader. As auditorias ambientais comprovam arquitetura/narrativa preservadas, world space, apoio, fade próximo e orçamento de partículas. Saves/preferências reais foram restaurados e o guard encerrado. Menu, escritório e GameplayRoot conservam SHA256.

Prévias nativas inspecionadas: `classroom-before.png`, `classroom-after.png`, `room-wide.png`, `school-entrance.png`, `road-entrance.png`, `road-cabin.png` e `road-arrival.png`. As fotos da escola usam luz temporária de inspeção equivalente à lanterna; a cabine e chegada usam a iluminação existente. A poeira antiga não reaparece em Play. Compilação encerrada e shaders suportados.

Para testar manualmente: iniciar a campanha pelo menu existente; no carro atender o telefone, observar a janela, pegar a lanterna e interagir com a porta para sair; subir os degraus e entrar. Na escola, explorar as salas e os corredores, verificar pistas próximas com a lanterna, desligá-la em área segura para comparar o visual e abrir as anotações/pausa. As passagens reservadas devem permanecer transitáveis; a bruma permanece no cenário ao mover o olhar.

Não foi produzido build de PC nem medida de FPS em uma máquina de jogador. Não declarar versão final para distribuição a partir deste polimento.
