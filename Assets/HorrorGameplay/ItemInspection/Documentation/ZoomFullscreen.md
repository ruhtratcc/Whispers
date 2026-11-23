# Inspeção — etapa 1: zoom em tela cheia

Esta etapa melhora somente o zoom e o posicionamento da interface. O painel **Ler texto** fica para a etapa seguinte, depois do teste solicitado pelo usuário.

## Como testar

1. Abra `SchoolCEEP`, entre em Play e examine uma fotografia ou documento com **E**.
2. Use o scroll para aproximar. A imagem cresce até cobrir a tela; título, descrição, rodapé e botões desaparecem gradualmente.
3. No zoom próximo, segure o botão esquerdo e arraste para percorrer a folha. O ângulo escolhido antes de aproximar é preservado.
4. Dê zoom para trás: a interface reaparece e a panorâmica retorna ao centro. Fora do modo próximo, o arraste continua girando o objeto.
5. No zoom próximo, o primeiro **Esc** retorna ao enquadramento normal. O segundo **Esc** guarda o item, mesmo se a primeira animação ainda estiver terminando. **E** guarda diretamente.
6. Confira **F**, pausa/retomada e a revisita pelo menu **Anotações**. Itens reabertos começam no enquadramento normal.

**R** continua reproduzindo as fitas nesta etapa. O botão de leitura ainda não foi acrescentado.

## Alterações

- `InspectionCameraController`: câmera e Render Texture acompanham o formato real do canvas; zoom próximo usa a resolução do viewport com limite de 2048 pixels no lado maior. No modo normal, o orçamento fica em aproximadamente um megapixel. A textura só é recriada quando a resolução ou o modo exigem outro tamanho.
- `InspectionUI`: controles em um grupo separado da imagem, fade proporcional ao zoom, expansão até o viewport, rodapé com espaços próprios para descrição, verso, detalhe e botões. O dimensionamento pela altura evita cortar botões em ultrawide.
- `InspectionManager`: Esc separado de E, retorno do zoom antes da saída e limpeza da inspeção caso uma nova alocação de textura falhe.
- Teste existente de teclado/mouse ampliado para panorâmica, fade, histerese, reaproveitamento da textura, Esc em duas etapas, pausa e Anotações.

O limiar de entrada do modo próximo é `Zoom01 >= 0,68`; a saída ocorre em `Zoom01 <= 0,48`. O fade é contínuo entre 0,40 e 0,68. Essa diferença evita alternar o modo repetidamente perto do limiar.

## Texto dos itens

Os campos editáveis `frontText` e `backText` já existem em **Create → Whispers → Inspection item**, com suporte a várias linhas no Inspector. Para novos itens, siga o [guia da inspeção](README.md). Os campos e inscrições não foram alterados nesta etapa; o painel de leitura será integrado depois com a mesma fonte de texto, sem manter duas versões divergentes.

## Verificação

Compilação externa de runtime, editor e testes concluída sem erros. Os três testes nativos passaram:

| Teste | Resultado | Tempo |
|---|---|---|
| Teclado/mouse + zoom | 1/1 | 6,44 s |
| Inspeção, coleta, Anotações e blackout | 1/1 | 5,07 s |
| Abertura da escola e progressão | 1/1 | 16,91 s |

Relatórios: `tests-input.json`, `tests-base.json` e `tests-opening.json`. Os testes da escola usam deslocamentos programados entre locais; não equivalem a percorrer toda a campanha manualmente.

As capturas em `Visual` usam render requests nativos do URP em 1920×1080, 1366×768 e 3440×1440. O canvas é temporariamente colocado em Screen Space Camera para renderizar no alvo correspondente, com coordenadas e proporções registradas. O pós-processamento da câmera principal é desligado apenas na composição da captura para manter a UI nítida. Isso verifica layout e imagem, sem constituir um benchmark do player final.

`Visual/zoom.json` concluiu as 13 capturas, com 26 render requests, zero erros e estado restaurado. Foram conferidos os três formatos, imagem normal/próxima e panorâmica. O checkpoint e preferências foram restaurados em `test-guard-restored-final.json`; a escola ficou limpa, fora de Play, com uma câmera e zero componentes visuais ausentes ou erros de narrativa/shader. Os cinco arquivos protegidos são conferidos por SHA256 em `protected-assets-final.json`.

FPS na build final e percurso humano completo da campanha não foram medidos nesta etapa.
