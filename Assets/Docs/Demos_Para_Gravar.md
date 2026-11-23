# Demos para gravar — cartões de tutorial

Nenhuma filmagem demonstrativa foi inventada. O jogo usa cinco imagens provisórias/originais e o modelo real da lanterna. O apito está configurado como vídeo, com imagem “DEMO APITO — GRAVAR” enquanto o campo Video estiver vazio. O grampo usa uma captura da interface atual; pode continuar assim ou receber vídeo.

## Formato e destino

- Windows: MP4, H.264/AVC, yuv420p, 960×540 (preferencial) ou 1280×720, 24 ou 30 fps, progressivo; 5–8 segundos em loop.
- Sem áudio nos vídeos. Os sons do cartão são separados. Se desejar áudio depois, há videoAudio no cartão, desligado por padrão.
- Exportar para Assets/Videos/Tutorial/demo_whistle.mp4 e demo_lockpick.mp4. Importar normalmente na Unity como VideoClip e ligar o campo video do cartão correspondente. Para o grampo, trocar mediaKind para Video.
- Usar um bitrate moderado, cerca de 1–3 Mb/s, mantendo texto e objetos visíveis. Evitar corte para preto no início/fim. A primeira e a última composição devem ser parecidas, permitindo loop discreto.
- A implementação usa VideoPlayer, Prepare, primeira imagem decodificada antes de trocar o fallback, Skip On Drop e UnscaledGameTime. Timeout de oito segundos ou erro volta à imagem sem bloquear o jogo.

## Como capturar

O pacote Unity Recorder NÃO está instalado. Não instalei dependência nova só para isso. Se você já usa um gravador de tela, capture apenas a Game View. Se preferir captura por quadros dentro da Unity, a instalação opcional é Window > Package Manager > Unity Registry > Recorder; depois Window > General > Recorder > Recorder Window. Não é necessário instalar para continuar jogando.

No Recorder, quando disponível: Movie > Game View > MP4/H.264; 960×540; 24 fps; áudio desligado; intervalo de duração 6–8 s; cursor e HUD de debug desligados. Se sua versão não exportar H.264, capture sequência de PNG e converta depois para MP4/H.264. Se o gravador de tela externo incluir cursor, desligue sua captura. Mantenha o FOV do jogo em 49.

Grave em um perfil de teste, com Dicas de item desligadas para não filmar o próprio cartão. Faça o mesmo preparo de flags/equipamento nos dois takes. Não edite o save pessoal para preparar a gravação. Não é necessário esconder contornos e efeitos que fazem parte da demonstração; esconda apenas debug, gizmos, janelas do Editor e o cursor do sistema.

## 1. Apito — demo_whistle.mp4 — REGRAVAR DEMO (03/11/2026)

Cena: Assets/SchoolCEEP/Scenes/SchoolCEEP.unity. Termine de ler as anotações do apito na biblioteca e feche a inspeção. A mecânica precisa estar descoberta para mostrar a resposta sobrenatural; faça o primeiro sopro/hesitação antes do take final, sem mudar essa cena.

Local sugerido: secretaria, olhando para “Pegadas pequenas na poeira”, posição aproximada do objeto (-16.40, 1.422, 29.35). Comece com os pés em torno de (-16.40, 0.65, 28.20), ajustando a distância sem encostar na mesa. Uma pilha ou objeto interagível próximo ajuda a mostrar o destaque do toque curto.

Sequência de 7 segundos:
1. 0–1 s: enquadramento parado com as pegadas pequenas no chão e um objeto interagível visível; apito já equipado.
2. 1–2 s: clique curto; mostrar o pulso e o contorno do objeto aparecendo.
3. 2–3 s: breve intervalo para o efeito assentar.
4. 3–5,5 s: manter a câmera parada enquanto o destaque permanece; não segurar o clique. Não filmar revelações do espelho nem aparições.
5. 5,5–7 s: manter o apito abaixado e voltar ao mesmo enquadramento inicial.

Não coloque texto de controle no vídeo: os glifos do cartão são dinâmicos e ficam fora da filmagem. Garanta que só o efeito visual mostre o pulso lento de um único clique.

## 2. Grampo — demo_lockpick.mp4

Cena: SchoolCEEP, arquivo de treino da secretaria, ID bd.lockpick.lock.0, móvel em (-18.50, 0.60, 30.20). Posição de partida sugerida dos pés: (-18.50, 0.65, 29.25), olhando para a gaveta superior. Pegue antes o grampo na gaveta original da secretaria.

Sequência de 6 segundos:
1. Abra o minigame antes de iniciar a gravação. Grave só a área do cilindro/pinos, evitando capturar o cursor do sistema e as bordas do Editor.
2. 0–1,2 s: mova o grampo devagar por um ângulo errado; aplique só um toque de tensão, sem quebrá-lo.
3. 1,2–2 s: encontre o ponto.
4. 2–6 s: mostre o cilindro girando com tensão e o clique do pino. Como o giro real dura 5,5 s, pode gravar 8 segundos para mostrar a conclusão sem acelerar a mecânica.
5. Termine com pequena pausa no resultado; corte o fim para o loop voltar de forma discreta à busca.

Se preferir, mantenha a imagem atual do minigame: não há obrigação de gravar esse segundo vídeo para o sistema funcionar. Sem música/narração; sons de tutorial não devem ensinar teclas por fala.

## Imagens e modelo

- flashlight: modelo 3D atual da lanterna compacta Poly Haven, respeitando o modelo antigo oculto; já funciona e gira com a câmera de inspeção reutilizada. Não precisa gravar vídeo.
- equipment_placeholder.png: diagrama original da HUD, sem revelar o terceiro item; pode ser substituído por uma captura sua da HUD.
- phone_placeholder.png: diagrama original do celular, sem texto de história; opcional substituir por captura da janela real, sem mensagem que revele fatos futuros.
- lockpick_placeholder.png: captura real da UI, recortada para o painel; pode continuar como imagem.
- whistle_placeholder.png: imagem identificada “DEMO APITO — GRAVAR”, substituída quando você ligar o vídeo.

O arquivo Assets/HorrorGameplay/Tests/Fixtures/Tutorial/codec_probe.mp4 é apenas um padrão técnico original de dois segundos, sem áudio, para testes automáticos de codec/loop/pausa/liberação. Ele NÃO está ligado aos cartões e não representa o jogo.
