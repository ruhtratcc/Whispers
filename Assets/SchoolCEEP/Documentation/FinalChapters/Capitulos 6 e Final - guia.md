# Capítulos 6 e Final — Whispers of Past

Os dois capítulos continuam a campanha na escola existente. Menu, prólogo, estrada e equipamento do João foram preservados. Nenhuma entidade tem AI, ataque ou colisão. Não há diálogo em voz gravada, perseguição literal ou música contínua.

## Capítulo 6 — O Terraço

Depois das memórias, volte à escada central. Suba os lances existentes, atravesse o patamar do segundo andar para a esquerda e continue pelos dois lances novos. As vozes aparecem como legendas durante a subida. Abra a porta metálica com E; atravessar fisicamente o vão é necessário para entrar no terraço.

A porta fecha apenas quando João já saiu do arco de movimento. João pode olhar e explorar durante a revelação. A presença fica imóvel, desaparecendo ao ser encarada ou ao desviar o olhar. Crianças ao fundo e lembranças revelam a promessa de João e o ciclo da escola. Depois da revelação, a porta libera o retorno e a criança indica a descida. Desça os dois andares da escada central até o corredor do térreo. O cartão do capítulo conduz ao Final sem recarregar a cena ou apagar itens.

O terraço aproveita a cobertura existente, com muretas nas bordas. A escada recebe vinte degraus de 17,55 cm, patamar de retorno e porta com folha volumétrica, batentes, fechadura e maçaneta. O antigo forro foi aberto por identificação exata de suas doze faces; a mureta que bloqueava o novo lance oeste também foi aberta. O restante da arquitetura original fica preservado. Pilhas e um caderno âncora estão sobre o banco do terraço.

## Capítulo Final — A Última Saída

Da escada, siga à direita pelo corredor central até o hall, depois volte pela entrada principal. A criança e o objetivo nas anotações indicam o caminho. A mensagem na parede muda com o capítulo, e a apresentação mantém a sensação de uma escola que quer João ali. O portão de entrada libera quando João realmente se aproxima por dentro; abrir o portão sozinho não conclui a saída.

Desça a escadinha e siga pelo caminho coberto. O telefone aparece na mão; espere o aviso e pressione E para atender. A chamada nunca é atendida automaticamente ao cruzar o volume. Depois dela, vá ao jardim à esquerda da entrada e olhe para a janela iluminada do último andar, acima da escada central. Uma observação contínua e sem parede bloqueando a vista dispara o encerramento. A tela termina em título/FIM, com retorno ao menu.

Há um bilhete âncora e pilhas no apoio junto à entrada. A janela e as entidades somem ao terminar a história. A saída mantém a dúvida sobre o que João viu; não há pontuação ou vitória por combate.

## Sistemas e controles

- WASD anda; Shift corre; mouse olha; E interage; F alterna a lanterna; I consulta objetivo/documentos; Esc pausa.
- Sanidade acompanha os dois capítulos, com choques únicos e recuperação pelas âncoras/lanterna. Não há uma barra grande de sanidade.
- Blackout apresenta GAME OVER. Tentar novamente volta ao início do capítulo atual, conservando os capítulos anteriores e o equipamento de entrada.
- Save/load preserva portão, porta do terraço, presença já desaparecida, itens coletados, intenção de atender a chamada e progresso. Sequências interrompidas retomam apenas o marco ainda pendente.
- Pausa suspende os efeitos e a observação, ocultando apresentações temporárias.

## Validação e limites

**31 testes PlayMode diferentes passaram, zero falhas nos relatórios finais:** seis na escola real para os capítulos 6/Final, sete de sistemas compartilhados, três do Capítulo 3, três do Capítulo 4, quatro do Capítulo 5 e oito de sanidade. Nesta retomada, os treze testes dos capítulos finais foram executados novamente após o ajuste da porta; os dezoito de regressão mantêm as provas da etapa anterior, sem mudança posterior nas fontes correspondentes. O teste de caminhada também passou isoladamente antes da suíte completa, sem contá-lo duas vezes.

O ajuste abre a porta durante a sequência que indica a descida, dando tempo para a folha terminar o movimento antes de devolver o controle. O teste reproduz o percurso com teclado e mouse, sem resetar o estado de agachamento para esconder uma colisão.

Resultados nativos ficam nos JSON desta pasta. A verificação geométrica cobre 268 amostras nos lances antigos/novos, patamares, terraço e caminho exterior; nenhuma ausência de piso ou bloqueio de altura. A vista para a janela foi provada pelos colliders e pelas faces dos meshes renderizados, do ponto do jardim usado pela história. Os testes de cena cobrem os dois capítulos, transições em cena, interação, checkpoint, pausa, sanidade e final. Um caso sobe os vinte degraus novos, cruza o patamar, abre a porta por E, entra e volta ao patamar original usando W/D/A e mouse, sem warps entre esses segmentos. Saves e preferências reais são protegidos e restaurados pelo guard de testes.

A auditoria final confirma SchoolCEEP salva e limpa, fora de Play, sete capítulos válidos, seis âncoras, sanidade inicial 100, luzes de evento apagadas, entidades ocultas, uma câmera, zero scripts ou meshes ausentes, zero inimigos e zero IDs duplicados. O guard foi encerrado depois de restaurar saves e preferências. As quinze fontes canônicas conferem com as instaladas por SHA256. Menu, escritório, estrada e GameplayRoot conservam seus hashes. A assinatura do restante da arquitetura original, excluindo somente a mureta aberta para o novo acesso, permanece igual. O console atual não registra erros de compilação ou de execução.

Ainda cabe ajustar ritmo/duração e tensão em uma sessão humana da campanha inteira, refinar as silhuetas e enriquecer a arte do terraço. Não foi gerado um executável de PC nem medida uma taxa de FPS. Os capítulos estão integrados ao projeto; isso não substitui o polimento final de uma versão para distribuição.
