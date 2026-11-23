# Estrada e rádio — 30/10/2026

Entrega aplicada no projeto Unity existente.

- Músicas passam de 12s para 24s por estação. Cada WAV contém 24,5s, evitando reiniciar o loop durante a estação. Cortes refeitos a partir dos previews públicos locais de 29,95s: excluem os 3s iniciais e escolhem a janela com mais energia e variação de energia. Offsets: 4 Cavala 3s, Definição de Pecado 5,4s, Turbulência 5,4s DENTRO DO PREVIEW. Não são timestamps das faixas completas. O julgamento musical de qual é a melhor parte não foi verificado por ouvido. Sem novos downloads.
- Duas reações do João em cada música, aos 4s e 14s; sete falas contando o desligamento. Legendas prontas, slots de áudio humano vazios, roteiro entregue. Não afirmar que foram dubladas; sem TTS.
- Dois faróis reais Spot reforçados, mantendo as posições fora do capô, intensidade1200, alcance65m, cone60graus, inclinação5graus e sombras suaves. Raios físicos dos dois alcançam o asfalto sem bater no próprio carro. Captura real revisada mostra o facho na pista.
- 36 casas nas laterais, com fachada/fundos/laterais/cobertura fechados, janelas escuras e algumas varandas discretas. Reuso da geometria de fachadas do escritório e material local Poly Haven WornCrackedPlaster; escritório sem alteração. Primeiras casas mais próximas para dar sinal de cidade logo no começo. Casas ao longe permanecem pouco visíveis, conforme solicitado. Sem novos colliders sobre a pista.
- Percurso passou de384m para1884m: início agoraZ=-1500, chegadaZ=384, escola/saída/portão preservados. Asfalto, terreno, acostamento e marcações estendidos. Na duração automática171s, média39,7km/h e cruzeiro41,8km/h. Começa em cruzeiro; últimos10% desaceleram suavemente. Trocar/desligar o rádio ainda pode antecipar a progressão, preservando a mecânica anterior.
- Motor: reaproveitado car_interior_loop.ogg gravado, ganho.18, passa-baixas1100Hz e variação leve de pitch. Pausa e parada interrompem corretamente o áudio. Nenhum motor sintetizado ou download.
- Volante original do Baird separado pela geometria nomeada Steeringwheel SilverCoronet do DAE local. 2536triângulos retirados SOMENTE da cópia do mesh da carroceria desta cena e reapresentados em mesh próprio, sem duplicação. Conservação exata de triângulos verificada. Fontes e prefab original intactos. Correções pequenas, até3,2graus; só o volante se move, e congela na pausa.

## Arquivos

- Assets/HorrorGameplay/Scripts/Narrative/RoadJourneyFlow.cs — cruzeiro/desaceleração, volante e pitch do motor.
- Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionRoadRadio.cs — segunda fala por faixa, sem repetição ao carregar checkpoint.
- Assets/RoadChapter/Scenes/RoadJourney.unity — percurso/casas/luzes/referências/configuração de áudio.
- Assets/RoadChapter/Models/CarPresentation/ — meshes separados e materiais derivados locais.
- Assets/Audio/Music/KentoRadio/Kento_*.wav — três recortes24,5s substituem os18s.
- Assets/HorrorGameplay/Tests/PlayMode/BeforeDemolitionRoadSceneTests.cs — duas regressões adicionais de cena real.
- Assets/ThirdParty/LICENCAS.txt e Assets/CREDITS.md — adendo dos recortes e derivações.

## Provas

48 testes únicos PASS: estrada15 + save/compatibilidade33. Incluem cada comentário em todas as faixas, restore sem repetição, pausa, volante em movimento, ambos faróis acertando a pista,36 casas fechadas, salvar/carregar na rota negativa e fluxo real de coleta/porta/chegada à escola. Compilação final sem erros. Story asset SHA256 igual ao backup. Nenhum script ausente, FOV49. Testes em diretórios temporários antes do Awake; save pessoal não acessado.

Editor devolvido à cena SchoolCEEP original, parado, limpo, sem override de save e com preferências de segundo plano restauradas. Nenhum ZIP. Backups e scripts em work/RoadCity20261030; aplicação já realizada, não repetir PatchRoad nem Workspace.End.

NÃO VERIFICADO: mix e cortes por escuta humana, conforto em uma partida inteira e build standalone. Novas falas/cantiga cantada continuam sem gravação humana. As músicas disponíveis continuam previews, não masters completos.
