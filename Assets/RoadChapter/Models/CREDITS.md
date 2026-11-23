# Modelos do Capítulo 2

O carro atual é um sedã próprio, criado diretamente no Unity por `DetectiveSedanBuilder.cs`. O modelo esportivo listado abaixo permanece como referência de procedência, sem uso na cena atual. A lanterna Torch continua sendo o único modelo externo utilizado no carro. Os arquivos externos foram obtidos de publicações públicas dos autores, sem login ou pagamento. Nenhum modelo foi gerado com Meshy.

## Sedã próprio de João — modelo atual

Geometria, materiais URP e textura pequena de tecido criados nativamente para o projeto. Fonte em `work/RoadChapter/Editor/DetectiveSedanBuilder.cs` e `Assets/RoadChapter/Editor/DetectiveSedanBuilder.cs`. Assets persistentes em `Assets/RoadChapter/Models/DetectiveSedan`. Não deriva do Spectral GT RS ou de outro modelo baixado.

Sedã comum de quatro portas inspirado em carros dos anos 80/90: carroceria verde-oliva escura, grade e para-choques cromados, rodas de aço, faróis retangulares, bancos de tecido, painel analógico, rádio de fita, vidros de manivela e maleta de equipamentos. A porta permanece estática; a interação coloca João diretamente fora do carro.

## Carro anterior, não utilizado: Spectral GT RS

Autor: **JaronKBragg7337**. Publicação e fonte: https://github.com/JaronKBragg7337/spectral-gt-rs. Licença: **CC0 1.0 Universal**, incluindo o modelo e a fonte original. O arquivo `SpectralGTRS/LICENSE` preserva a declaração do autor. A licença permite uso comercial e modificação, sem exigir atribuição.

Download original: https://raw.githubusercontent.com/JaronKBragg7337/spectral-gt-rs/main/spectral_gt_rs_game_ready.glb.

`SpectralGTRS/SpectralGTRS.obj` e seu MTL são uma conversão estática desse GLB para importação nativa no Unity. Os nós e materiais foram preservados por nome. A conversão aplica os transforms originais, mantém metros e coloca a frente em **+Z**, com **Y para cima**. Não preserva animações; as quatro animações originais permanecem no GLB. Não há imagens ou texturas externas no GLB. O MTL representa cores e uma aproximação de brilho; os parâmetros PBR completos estão em `SpectralGTRS.Materials.json`.

Dimensões convertidas: **2,526 m de largura, 1,680 m de altura e 5,207 m de comprimento**. São 151.722 triângulos, 258 grupos de geometria e 23 materiais. Há bancos, painel, volante, espelhos, portas, rodas, faróis e detalhes mecânicos.

Esse carro esportivo foi usado apenas na revisão anterior da estrada, com adaptações de volante, teto, vidro e dobradiça. Foi substituído integralmente pelo sedã próprio a pedido do usuário. Seus arquivos e créditos permanecem como histórico, sem referência ao modelo na cena atual.

## Lanterna escolhida: Torch

Autor: **Quaternius**. Publicação do modelo: https://poly.pizza/m/WGsvr4KOZd. A página identifica o arquivo como **Public Domain (CC0)** e fornece a licença https://creativecommons.org/publicdomain/zero/1.0/. Download público original: https://static.poly.pizza/035c4897-22f3-4e9c-b29f-ebafe2b566da.glb.

`QuaterniusTorch/Flashlight.obj` e seu MTL são uma conversão estática do GLB. O objeto é uma lanterna elétrica com corpo amarelo/preto, botão e lente azul. A conversão aplica os transforms originais, centraliza o volume e normaliza seu comprimento para **0,26 m**, com a lente na direção **+Z**. A largura é aproximadamente 0,070 m e a altura 0,069 m. São 610 triângulos e quatro grupos de material: `Yellow`, `Black`, `LightBlue` e `DarkYellow`. Não há texturas ou animações. O arquivo `Flashlight.Materials.json` preserva os parâmetros dos materiais originais.

## Alternativas baixadas

- **Kenney Car Kit 3.1**, https://kenney.nl/assets/car-kit, CC0. O pacote original e seu `License.txt` permanecem em `KenneyCarKit`. Inclui FBX/OBJ/GLB de sedãs, mas a inspeção não identificou cockpit separado no sedã. Foi substituído na escolha pelo Spectral GT RS.
- **Flashlight, Robert Ramsay**, https://poly.pizza/m/bJaT8R5j3uD, **CC BY 3.0**, https://creativecommons.org/licenses/by/3.0/. O GLB permanece em `RobertRamsayFlashlight` como alternativa. Não foi escolhido. Se usado, atribuir “Flashlight — Robert Ramsay”, informar a licença e indicar modificações.

## Preparação e verificação

`work/RoadChapter/inspect_models.py` lê dados GLB e gera metadados de geometria. `work/RoadChapter/export_downloaded_models.py` faz a conversão estática. Nenhum script incluído nas fontes dos modelos foi executado. Os arquivos `.inspection.json` e `.conversion.json` registram triângulos, bounds, grupos e nomes de materiais. `provenance.json` registra fontes e hashes dos arquivos escolhidos.

As conversões externas foram verificadas por leitura dos índices, faces, posições e normais. O sedã atual possui meshes e materiais nativos persistentes, agrupados por material. As prévias externas, da cabine e da chegada foram inspecionadas. Nesta revisão, a validação no Unity Editor aprovou 12 testes PlayMode da estrada, incluindo acompanhamento do banco durante o deslocamento, coleta da lanterna sem saída automática, clique esquerdo na porta e teleporte imediato mantendo a porta fechada. Relatórios em `../Revision3`. Os três testes da câmera compartilhada permanecem registrados na revisão anterior. Nenhuma build de PC ou medição de FPS foi executada.
