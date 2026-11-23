# Escola — primeira sequência jogável

O pedido de 07/10/2026 autorizou começar a implementar a progressão e a ambientação de terror. Esta entrega monta o capítulo **A Escola Abandonada** na cena existente `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity`, até o desenho e o apagão. O roteiro original e os sistemas de narrativa, inventário, legendas e checkpoint continuam como base.

## Caminho da investigação

1. Entre pela escadinha frontal. O portão duplo começa aberto e fecha atrás de João depois que ele passa completamente pelo giro das folhas.
2. Tente abrir o portão. As duas folhas ficam bloqueadas e a investigação passa a ser o objetivo.
3. Procure os documentos sobre a mesa da secretaria. A leitura abre a nota; feche-a para continuar.
4. Examine a fotografia ao lado. A legenda mostra “Alguns nunca foram embora.”
5. Explore as salas do corredor central. Na Sala 02, observe o quadro: “JOÃO, VOCÊ VOLTOU.”
6. Vire para a entrada da sala/corredor. Uma batida soa e a inscrição desaparece.
7. Na Sala 03, aproxime-se da criança. Ela desaparece e deixa um desenho no chão.
8. Examine o desenho. Ele entra no inventário e a legenda mostra “NÃO SUBA.”
9. As luzes locais se apagam e aparece a conclusão do capítulo.

Os objetos só avançam a história na etapa correta. O quadro e a criança exigem observação com linha de visão física. O fechamento do portão não cria uma parede invisível nem teleporta o jogador. A retomada do checkpoint reconstrói pistas, inscrição, aparição e entrada sem repetir os sustos concluídos. Uma legenda interrompida da fotografia ou do desenho pode ser retomada sem duplicar o item.

## Ambientação

A escola recebe um perfil próprio de imagem: granulação, linhas finas, leve separação de cores, suavização e uma curta falha de tracking. Interferências breves acompanham os acontecimentos. Não há contador, indicação REC ou interface de gravação. Legendas e HUD continuam fora do filtro.

O hall mantém iluminação suficiente para orientação, com tons discretamente frios e esverdeados. Uma luminária distante pode sofrer uma queda suave de intensidade. Sons curtos de madeira e metal vêm de cinco regiões reais da escola, separados por longos intervalos de silêncio. O relógio desses sons congela durante pausa e sequências que bloqueiam os controles. Não há novo chiado contínuo nem voz de diálogo. A respiração gravada já escolhida pelo usuário e os sons de interação/passos permanecem.

## Acesso e limites desta entrega

Abra a cena SchoolCEEP no Editor e inicie o Play para testar a escola desde sua entrada. Novo jogo no menu continua começando no escritório; o mapa da estrada ainda não existe. O apagão encerra esta sequência: o próximo capítulo, a chave antiga, a perseguição, as memórias e o terraço continuam futuros. Nenhum mapa substituto é carregado como se esses capítulos estivessem concluídos.

A fotografia é um objeto ilustrativo inicial e a criança usa um modelo simples de aparição, ainda sem arte final ou animação. A Automotiva permanece fechada e não contém uma pista obrigatória.

Os passes `SchoolHorrorAtmospherePass` e `SchoolOpeningPass` fazem parte do construtor da escola, para preservar a montagem em futuras reconstruções. Os novos objetos narrativos ficam fora do agrupamento estático. A ambientação não acrescenta colliders à circulação; somente as folhas reais do portão, o volume de entrada e os pequenos alvos das pistas têm física.

## Verificação

A instalação na cena salva confirmou os nove marcos, três pistas, portão pareado, volume fora do giro, referências do capítulo, ausência de colisão na aparição, perfil exclusivo da escola, cinco pontos sonoros, seis luzes de falha e shader sem erros. Capturas nativas mostram a iluminação e o VHS.

Passaram **13 testes focados**: seis EditMode para o efeito e a ambientação, seis PlayMode isolados para ordem, pausa, linha de visão e retomada silenciosa, e um PlayMode na escola real. Este último lê as três pistas pelo raycast do jogador, verifica o portão, observa o quadro/corredor e a criança, conclui os nove marcos e grava o desenho e o apagão. A fixture usa posições suportadas e deslocamentos seguros entre os pontos; não equivale a um percurso contínuo ou teste de perseguição.

Os testes reais encontraram e corrigiram o ponto de observação atrás do batente da Sala 02 e a colisão do próprio corpo de João na observação para baixo. A verificação final foi feita depois de reabrir a cena salva. O checkpoint anterior e suas preferências foram restaurados; a cena ficou limpa, fora do Play, com uma câmera. Relatórios: `School-opening-tests.json`, `School-opening-installation.json` e `School-opening-final-state.json`.
