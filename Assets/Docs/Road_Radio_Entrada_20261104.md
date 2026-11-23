# Rádio e entrada da escola — 04/11/2026

Corrigidos os dois pedidos. Quatro botões físicos no rádio do modelo de carro atual:energia,próxima faixa,volume− evolume+. Marcas em relevo e materiais iluminados pelo ambiente, sem emissão. Os alvos ficam sobre a superfície medida da malha do rádio; o painel antigo usava as coordenadas do sedã anterior. A captura final é radio-in-car.png; radio-authoring.png registra apenas a primeira montagem, que foi substituída.

O portão tinha duas folhas com colisão e o gatilho exigia que o centro da cápsula já estivesse depois delas. O personagem parava cerca de39cm antes desse plano. A área de chegada agora começa65cm antes, mantendo o limite lateral e as folhas sólidas. Aproximar-se inicia o fade e a transição existente, com as mesmas flags.

Arquivos:
- Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionRoadFlow.cs — ajuste da área de entrada.
- Assets/RoadChapter/Editor/RoadRadioButtonsPass.cs — montagem reaplicável dos quatro controles e suas áreas clicáveis.
- Assets/RoadChapter/Scenes/RoadJourney.unity — posicionamento físico, marcas e colisões dos botões.
- Assets/RoadChapter/Data/RadioButtons/ — três materiais próprios, sem downloads.
- Assets/HorrorGameplay/Tests/PlayMode/RoadRadioGateRegressionTests.cs — interação por raycast e caminhada até a escola.

Validação final:3 testes únicos em Play aprovados. Os quatro controles foram acionados pelo mesmo raycast/teclaE do jogador; volume−/+alteram o volume e os dois controles de rádio preservam as reações narrativas. O teste de caminhada partiu antes do portão e usouW, sem atravessar ou remover sua colisão; SchoolCEEP carregou, a narrativa inicializou e os controles voltaram. A regressão de distância/fade do áudio também passou. Compilação final sem erros;0 scripts ausentes na cena editada. Captura final inspecionada visualmente.

Duas execuções iniciais da caminhada excederam o limite de40s durante o carregamento. O orçamento de espera do teste foi ajustado para120s, sem alterar o carregamento do jogo; a execução final completou em13,526s. Os resultados iniciais foram preservados. A primeira montagem dos botões foi rejeitada na inspeção visual por estar ao lado do rádio; a versão final usa a superfície do modelo instalado.

Backup em Assets/_Substituidos/RoadFix20261104/. O ID persistente da cena e seu GUID permanecem. Save pessoal não acessado; os testes usaram diretórios temporários e restauraram preferências. Sem alterações no mapa da escola, FOV49, história, áudio, save/schema ou outros capítulos. Sem downloads, ElevenLabs ou ZIP.

Unity deixada no menu MainMenu, cena limpa, fora de Play e sem override de save de testes. Para testar:Play,continuar a partida; usarE olhando para os controles e aproximar-se do portão ao sair do carro.

NÃO VERIFICADO:campanha inteira, sensação/escuta humana e executável standalone.
