# Sistema de falas reativas — DESIGN APENAS

Nenhum script de runtime, ID de história, flag ou save foi alterado. A planilha é um banco de opções, não uma sequência para tocar inteira. Estado EXISTE significa texto/evento existente; NOVA-OPCIONAL é só proposta. Slots “PRECISA CRIAR” não existem no jogo. As condições em português especificam a futura integração e não são código copiável.

## Um único árbitro de fala

Reutilizar o NarrativeDirector e os bloqueios de áudio/legenda existentes, após conferir suas interfaces. Ordem: história/fita/interfone > dica de objetivo > reação > ambiente. P1/P2/P3 é prioridade de produção/gravação, não licença para uma reação cortar uma fala de história. Só uma fala inteligível por vez. Um evento essencial suspende barks e descarta os atrasados; nunca interromper uma fita para dizer “preciso ouvir”.

Elegibilidade antes de sortear E antes de tocar: capítulo, objetivo, item, progresso e percepção reais continuam iguais; sem pausa/menu, blackout, transição, inspeção, PC, celular ou legenda importante. Exceção: grupo PC específico pode concorrer apenas durante momento livre do próprio PC, sem voz importante, app fechando/abrindo ou ruído-chave. Não usar uma dica genérica dentro do PC. A voz infantil não pode substituir a pista que a trama ainda não apresentou.

Cooldown global de João: mínimo 12s depois do FIM do último clip; 18s após evento forte. Os cooldowns por grupo da aba Falas_Reativas são maiores e acumulam. Outras vozes respeitam o mesmo árbitro e pelo menos 12s de silêncio entre falas. Teto inicial: uma reação na entrada e uma dica útil por visita; no máximo duas dicas de objetivo por área a cada 3 min; uma fala de ambiente por área a cada 5 min. Dicas nunca suprimem bateria crítica: priorizar a utilidade e descartar a menos importante. Ajustar pela escuta, preservando o silêncio.

## Sorteio sem repetição e relevância

Por grupo e nível, embaralhar uma bolsa de IDs elegíveis; retirar um por vez. Ao esvaziar, reembaralhar e impedir que a primeira seja a última tocada. Se só há uma linha, obedecer Pode repetir e o limite por área. Reutilizações do mesmo ID em grupos diferentes compartilham lastPlayed; deduplicar por ID, não por linha da planilha. Uma fala não vira evento novo só porque foi gravada.

Reações ligadas a ações expiram em até 3s; não guardar “Abriu” para depois de o jogador sair. Dica em espera expira em 5s e é revalidada no objetivo; preferir recalcular a próxima candidata em vez de manter uma fila. Falas de final só se houver janela real depois das falas canônicas; nunca acrescentar uma cena para acomodar uma variante. Ao pausar/carregar/transicionar, esvaziar fila e reiniciar contadores transitórios; não disparar todas as reações restauradas de um save.

## Três níveis de dica

Tempo ATIVO sem progresso, descontando leitura, áudio, pausa, inspeção e PC. L1 após 75–90s: observação vaga. L2 após 150s: lembrar pista já descoberta. L3 após 240s: direção clara do objetivo atual. No máximo uma variante por nível por objetivo/visita, respeitando limite por área. Progresso real zera o cronômetro; andar em círculo não zera. Pedir ajuda no futuro poderá adiantar um nível, mas não existe botão novo neste pacote.

Algumas pistas já estão no email obrigatório; mesmo assim a fala que as cita exige aquele email lido. Para relatório, culpa e apito: não antecipar conhecimento do jogador. “Eu devia ter respondido.” é uma única vez, após email 1 + relatório, nunca sorteada como comentário genérico. As variantes de revisita que dizem “já vi”, “pela metade”, “mudou” ou nomeiam um detalhe exigem respectivamente inspeção anterior, reprodução interrompida ou observação antes/depois. Sem esse comprovante, a variante é inelegível; pode restar só uma variante no grupo.

## Portas, pilhas e casos específicos

Porta trancada: primeira tentativa voluntária, não restauração de locked em save. Repetida: terceira tentativa na mesma porta em 60s; uma reação, não três. Destrancar: mudança real true→false causada pelo jogador. Chave encontrada genérica e reação do objeto concorrem; apenas uma. Não dizer “Fechada por dentro” quando o dado só indica trancada.

Bateria 35%, 15%, 5% e zero: cruzamento descendente, uma vez por descarga. Rearmar cada patamar só após recarga acima dele +5 pontos. Pegar pilha exige coleta confirmada; “voltou a luz” exige lanterna acesa depois da recarga; não basta inventário aumentar. Falha sobrenatural exige efeito percebido. Não disparar por olhar pilhas através de parede. No escritório a lanterna permanece indisponível.

Apito: antes de soprar só se o estado permite uso; não atrasar input. Um bark antes de no máximo um em cinco sopros. Eco correto exige resposta efetivamente tocada/percebida, nunca usar bark para esconder falha de áudio. Sem eco exige terminar a janela inteira. Apito já é gravação real, não assobio humano. Não dizer “único que sobrou”: existem outros objetos das crianças.

## Gatilhos ainda não implementados

Todos os barks são design, mas “Depende de mecânica futura” marca uma exigência além de ligar o bark a um estado existente. LOCKPICK, seleção manual de chave errada, tropeço autorado e agarrar/mover objeto não são confirmados no fluxo atual: FUTURO, desligados. Contadores de tentativas, visitas, tempo e lastPlayed seriam estado transitório do próprio bark, sem criar flags narrativas. Eventos condicionais de porta/cadeira/gaveta só ficam elegíveis se o evento existente realmente ocorreu e foi visto; não instalar um evento novo por causa da fala.

## Slots e legendas — por que não há importador agora

Há AudioClip direto, SoundSet, mensagens e emails silenciosos, três montagens com vários intérpretes e três fitas com captionOnlyRecording=true. Um importador que apenas preenche campos daria áudio errado ou continuaria silencioso nas fitas. Por isso entregamos Fala_vs_Clip_Slots.csv com ligação exata, não um importador que mexe em modo/flags. Não preencher arrays de texto como áudio. Gravador antigo e antiga história permanecem arquivados.

Depois dos takes, duração da legenda >= max(1,5s, caracteres/15, duração do clip). Para falas existentes conferir também o controle do NarrativeDirector e o encerramento das inspeções. Não acelerar voz para caber. As legendas dos barks propostos ainda precisam ser criadas, com acessibilidade; vocalizações podem usar [respiração] quando relevantes. A checagem está em Duracoes_legendas.csv. Nada foi alterado agora.

## Critérios para uma implementação posterior

Provar: nenhuma sobreposição; não há dicas em pausa/carregamento; salvar/carregar não repete eventos; revelações não antecipadas; cada ramo final usa só suas linhas; bateria sobe/rearma corretamente; agrupamentos sem repetição; chave/cadeira/gaveta exigem ação real; terceira repetição do rádio preservada; todas as variantes FUTURO desligadas. Testar com perfil descartável, preservando save pessoal.
