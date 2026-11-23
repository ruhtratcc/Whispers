# Apito metálico e visão através das paredes — 31/10/2026

Concluído no projeto `C:/Users/ruhtra/My project`.

## Comportamento
- Na escola, após a descoberta já existente, Q equipa e o botão do meio sopra. Curto/longo continuam funcionando.
- Todos os interagíveis próximos e disponíveis ganham destaque através das paredes, incluindo portas, pistas, itens de inspeção e as 12 pilhas. Alcance de 24 m; efeito temporário de 3,6 s. Pilhas dentro da gaveta fechada também aparecem.
- Objetos coletados, desativados ou ainda indisponíveis deixam de aparecer. Não revela as crianças/Helena; no espelho, apenas o vidro é destacado. Escritório e estrada continuam com apito comum.
- 319 componentes de interação cobertos em 187 grupos físicos. A conferência final não encontrou meshes com dois donos de destaque, referências inválidas, scripts ausentes ou erros de shader. FOV 49 preservado.

## Som
Gravação real [metal whistle.wav, strongbot](https://freesound.org/people/strongbot/sounds/568995/), CC0 1.0. Utilizado o preview HQ público MP3, sem login; não o WAV original. Recortes mono: sopro curto de 0,43 s e trecho sustentado de 0,65 s com crossfade de loop. Ganho e fades moderados; sem síntese ou TTS. Sopro e eco ligados à configuração existente. Fonte, cortes, hash e licença em `audio-provenance.json` e `Assets/ThirdParty/LICENCAS.txt`. Originais anteriores preservados.

## Alterações
- `WhistleHighlightable.cs`: disponibilidade real dos interagíveis; todas as categorias atravessam paredes; coleta/expiração encerram o destaque; conteúdos de gavetas visíveis.
- `WhistleSense.cs` e `WhistleConfig.cs`: cobertura ampliada de 24 para até 512 alvos, buffers pré-alocados de 512; alcance mantido. Sem busca global por cena a cada frame.
- `WhistleOutline.shader`: passe único com comparação contra a profundidade da câmera. Corrige o segundo passe antigo, que não desenhava a silhueta oculta no teste real. Também cobre oclusão parcial.
- `SchoolCEEP.unity`: marcação nativa de todos os interagíveis, sem modificar layout, história ou mecânica de portas. Eliminados destaques duplicados de objetos dentro da gaveta e da porta já coberta.
- `WhistleConfig.asset`, `RecordedEcho.asset` e `Audio/SFX/Whistle/Metal/`: nova gravação e configuração.
- `WhistleXrayPlayTests.cs` e três testes existentes: cobertura, disponibilidade, limite antigo, drawer, GPU e evidências desta entrega.

## Validação real
25 testes únicos passaram: 11 EditMode, 10 de descoberta/regressão e 4 de x-ray. Ver `test-summary.json`. A primeira captura GPU não mudou nenhum pixel: confirmou a falha do shader antigo. Após a correção, a diferença média foi 0,008030592, com uma pilha azul visível atrás de uma parede opaca; `wall-before.png` / `wall-xray.png`. O teste também confirmou que o AudioSource começa a reproduzir a gravação nova e que os clips carregam com sinal não nulo.

A regressão da escola fez leitura/descoberta, gaveta, pilhas, efeitos, a pista da secretaria e salvar/restaurar usando saves descartáveis. O teste de escritório confirmou ausência de efeitos sobrenaturais fora da escola. Não houve crescimento de renderers/fontes nos sopros repetidos medidos. As falhas iniciais e repetições permanecem em `work/WhistleXray20261031/test-runs.json`; o resultado final usa o último resultado por caso.

Unity restaurada na cena SchoolCEEP, limpa e fora do Play, background original restaurado e override de save de teste nulo. Save pessoal não utilizado; nenhum ZIP.

## Como conferir
1. No progresso da escola com o apito descoberto, equipe com Q e pressione o botão do meio.
2. Procure contornos de portas, pistas e pilhas atrás de paredes próximas; devem sumir em 3,6 s.
3. Colete uma pilha descarregando a lanterna antes: ela não deve reaparecer no próximo sopro.
4. Segure o botão para o sopro sustentado; pause e abra inspeção/PC/celular para conferir os bloqueios existentes.

NÃO VERIFICADO: mix por escuta humana no seu headset, sensação durante campanha humana completa e build standalone. Capturas GPU e a captura da biblioteca foram inspecionadas; isso não substitui avaliar a sensação em jogo.
