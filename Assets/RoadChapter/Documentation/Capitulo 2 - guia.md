# Capítulo 2 — O Caminho até a Escola

Esta entrega acrescenta uma transição jogável entre o escritório e a escola. João viaja de carro, recebe uma ligação, observa uma janela acender, pega a lanterna, interage na porta e caminha até o portão. O capítulo termina no cartão de conclusão. O capítulo seguinte aguarda aprovação do usuário.

## O que foi implementado

A viagem é automática e dura 38 segundos. O jogador pode olhar ao redor da cabine enquanto acompanha as legendas do rádio sobre a escola abandonada e os desaparecimentos. A posição da câmera acompanha o banco a cada frame, sem o atraso em posição mundial que fazia a câmera atravessar o carro. A rotação continua suave. A pausa congela o deslocamento e os eventos.

O carro estaciona, o telefone toca e João pode atendê-lo. A ligação apresenta os sons de crianças de forma descritiva e “Você demorou.” por legendas. Depois da ligação, uma janela da escola acende. Olhar para ela com linha de visão confirma o acontecimento e permite pegar a lanterna no banco.

Pegar a lanterna liga o equipamento e mantém João sentado. O novo objetivo pede sair pela porta do motorista. Clicar na porta é uma interação separada: João é teleportado diretamente para o ponto externo e os controles de caminhada são liberados. A porta permanece fechada, sem animação. O limite atrás do carro é uma barreira invisível com colisão, sem uma parede visível. O jogador caminha pelo passeio até o portão e interage para concluir o capítulo.

A conclusão mostra “CAPÍTULO 2 CONCLUÍDO”, “Alguém sabia que ele chegaria.” e “CAPÍTULO 3 — A Escola Abandonada”. A cena permanece nesse cartão, com opção de voltar ao menu; ela não carrega automaticamente a escola nesta entrega.

Há iluminação noturna, névoa, faróis e tratamento VHS discreto. Uma luz suave na cabine torna os objetos legíveis. O motor utiliza uma gravação em volume baixo durante o deslocamento e para ao estacionar. O telefone usa os sons gravados existentes. Não há música contínua, voz de diálogo, combate ou perseguição.

O ritmo previsto é de aproximadamente 70–90 segundos ao seguir o objetivo, sem contar pausas ou tempo adicional de observação. Esse intervalo é uma estimativa, não um limite imposto ao jogador.

## Modelos integrados

O carro é um sedã antigo próprio, criado com geometria nativa no Unity: carroceria escura, quatro portas, cromados, rodas, faróis, bancos, painel analógico e detalhes de uso. O modelo esportivo anterior não é usado na cena. A lanterna continua usando [Torch, de Quaternius](https://poly.pizza/m/WGsvr4KOZd), publicada sob CC0 e convertida para OBJ com materiais URP. Nenhum modelo foi gerado com Meshy.

A cabine foi montada para a câmera do banco: volante, teto, pilares, painel, console e objetos respeitam seus espaços e linhas de visão. As janelas têm aberturas livres. A interação da porta cobre seu painel interno e a maçaneta. A lanterna fica sobre a maleta no banco do passageiro e aparece na mão depois da coleta. Essas adaptações pertencem somente à cena da estrada; o prefab compartilhado foi preservado.

Fontes, licenças, conversões e hashes estão em Models/CREDITS.md e Models/provenance.json, nesta pasta e no projeto Unity.

## Controles

| Controle | Uso |
| --- | --- |
| Mouse | Olhar ao redor, inclusive dentro do carro; mirar objetos interagíveis. |
| Clique esquerdo / E | Atender o telefone, pegar a lanterna, sair pela porta e interagir com o portão. Cada ação é separada. |
| W / A / S / D | Caminhar depois de sair do carro. |
| F | Ligar ou desligar a lanterna depois de pegá-la. |
| I | Consultar o inventário existente. |
| ESC | Abrir a pausa durante o jogo; no cartão final, voltar ao menu. |

O objetivo atual aparece no HUD. As interações mostram uma indicação ao mirar o objeto de perto. Não há direção manual, aceleração ou frenagem do veículo.

## Como testar

1. No Unity, abra Assets/RoadChapter/Scenes/RoadJourney.unity e dê Play. Outra opção é iniciar um novo jogo pelo menu e concluir o escritório; a saída do prólogo leva à estrada.
2. Observe a viagem e as legendas. Olhe para os lados e para baixo: a câmera deve permanecer dentro da cabine enquanto o carro anda. ESC deve pausar a viagem e as legendas.
3. Ao estacionar, mire o telefone e pressione E. Espere a ligação terminar.
4. Olhe para a janela iluminada da escola. Mire a lanterna no banco e pressione E. João deve continuar sentado com a lanterna; F deve funcionar.
5. Olhe para a maçaneta da porta do motorista, à esquerda, e clique com o botão esquerdo (E também funciona). João deve aparecer imediatamente no ponto de saída; a porta permanece fechada. Atrás do carro, o limite de circulação deve impedir passagem sem exibir um muro.
6. Caminhe até o portão, mire e pressione E. Confira os cartões do Capítulo 2 e do próximo capítulo. A escola não deve carregar automaticamente. Use a opção de retorno ou ESC para voltar ao menu.

O checkpoint guarda sete marcos narrativos. Ao retomar, acontecimentos concluídos não se repetem. Um save novo feito depois de pegar a lanterna mantém João sentado até a interação com a porta. Saves antigos que já estavam na caminhada recebem o novo marco de saída silenciosamente e mantêm a posição externa.

## Validação registrada

Nesta revisão do sedã, em 07/10/2026, **12 testes PlayMode da estrada passaram, zero falhas**: [Revision3/road-tests.json](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/Revision3/road-tests.json>). Os três testes da câmera compartilhada passaram na revisão anterior, registrada em Revision2; sua fonte não mudou nesta revisão.

Os testes da estrada verificam câmera durante o deslocamento, pausa, ordem dos eventos, linha de visão da janela, alcance real das quatro interações por raycast, coleta sem saída automática, clique esquerdo na porta, teleporte imediato com a porta fechada, barreira invisível e caminhada contínua com W até o portão. Também verificam saves sentado e andando, migração dos saves antigos, capítulo concluído e transferência dos equipamentos/bateria do escritório pelo mecanismo real de passagem entre capítulos. A conclusão permanece na estrada.

A comparação em [Revision3/protected-after.json](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/Revision3/protected-after.json>) confirma SHA256 inalterado de MainMenu, Chapter1Office, SchoolCEEP e GameplayRoot. Os testes usam saves descartáveis; arquivos, preferências e configurações de entrada foram restaurados.

[Revision3/final-scene.json](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/Revision3/final-scene.json>) registra a cena salva, uma câmera, quatro interações, zero inimigos, zero scripts ausentes, narrativa válida, geometria nativa do sedã e estado inicial restaurado.

Prévias nativas revisadas: [car.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/car.png>), [car-rear.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/car-rear.png>), [drive.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/drive.png>), [arrival.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/arrival.png>), [cabin.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/cabin.png>) e [gate.png](<C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadChapter/gate.png>). O HUD foi ocultado. As duas vistas externas do carro usam uma luz temporária de inspeção, retirada ao terminar a captura; a iluminação do jogo permanece noturna.

## Limites e próxima etapa

A fachada exterior serve à chegada deste capítulo e não substitui o mapa completo da escola. O veículo percorre um trajeto programado; não há simulação de direção. O sedã próprio foi modelado para esse percurso e sua interação de entrada/saída.

Os diálogos permanecem somente em legendas. Sanidade, blackout/Game Over no início do capítulo, diretor dinâmico de terror e abandono completo da escola pertencem às etapas futuras aprovadas. Nenhuma entidade persegue ou ataca o jogador neste capítulo.

Os créditos da gravação CC0 do carro estão em [Audio/CREDITS.md](<C:/Users/ruhtra/My project/Assets/RoadChapter/Audio/CREDITS.md>). Os créditos do telefone estão em [NarrativeAudio-Credits.txt](<C:/Users/ruhtra/My project/Assets/StreamingAssets/NarrativeAudio-Credits.txt>).

Não foi gerada uma build de PC nem medido FPS em hardware de referência. A validação descrita é da cena e do runtime no Unity Editor.
