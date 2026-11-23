# Grampo — configuração, teste e manutenção

## Ajustes

`Assets/HorrorGameplay/Data/Lockpick/LockpickConfig.asset`: inicial 2, limite 6; tolerâncias fácil/média/difícil 18°/12°/9°; giro 5,5 s por pino; stress 0,22/s e recuperação 0,16/s. O stress varia com a distância do alvo. Assistido: +25% tolerância e stress ×0,45. Três quebras na mesma tranca: +15% tolerância. Automático: segurar continuamente 9/18/27 s, consumindo um grampo no final. Cancelar não consome, soltar recupera stress; quebrar consome um e preserva pinos já assentados. Desbloqueio normal não gasta grampo.

Sinergia com apito: toque curto até 5 m, após descoberta sobrenatural, uma vez por tranca; dica fraca por 1,8 s ao começar e ao assentar cada pino da mesma tentativa. Cancelar não renova a dica. Legendas de som preservadas junto às falas do grampo. Sem dano à sanidade, perseguição ou ataque.

Ganhos: raspar 0,18; clique/trava/destrancar 0,20; quebra 0,20; vozes espaciais 0,16. SFX e Voice usam o mixer existente. Sussurros a cada 16 s, com bloqueio entre falas; pensamento contextual após 20 s e depois 24 s. Helena somente após 35 s ou duas quebras, intervalo global de 100 s. Reduzir efeitos remove vibração e falha visual; vibração máxima 0,7 pixel.

Opções/Pausa > Equipamentos: arrombar com ajuda e arrombar automaticamente; ambos desligados por padrão. Manter lanterna acesa respeita a opção existente. HUD: slot 3 após coleta, xN, AGUARDE na troca, SEM GRAMPO quando zerar. Sem HUD antes da lanterna, inclusive no escritório.

Controles: E interage; 3/roda equipam o grampo. Q passa a alternar equipamentos após obter o grampo (antes preserva o apito). Mouse horizontal ajusta o ângulo; segurar clique aplica tensão. Esc cancela; botão Pausar ou F10 abre a pausa. Câmera, movimento, celular e uso do apito ficam bloqueados enquanto a interface está aberta.

## Nova tranca em três passos

1. Escolha só um móvel/porta opcional fora da rota obrigatória. Preserve a porta original com InteractiveDoor ou use InteractiveDrawer. Defina um ID persistente único; não use porta pareada, com chave ou SchoolAreaRestriction.
2. Adicione LockableDoor, tipo OptionalPickable; ligue door OU drawer, dificuldade 1/2/3, capítulos, flag opcional e tolerância sobrescrita se necessário. O campo requiredKey da InteractiveDoor continua atendendo às portas por chave. Nunca transforme uma porta principal em opcional.
3. Ligue o grupo de recompensas inicialmente inativo, com IDs próprios; teste com zero grampos, cancelar, quebrar, salvar/carregar e a rota principal. Notas propostas só podem ser ligadas depois da aprovação. Em nova cena, reutilize config/view e ligue GameSession.lockpick ao sistema.

## Checklist no Play

- Escritório e carro: sem grampo; controles e equipamentos anteriores preservados.
- Capítulo 3: abrir a gaveta original da secretaria, pegar o pequeno grampo ao lado da lancheira; começa com dois. Reserva no lado oposto somente após a coleta inicial.
- Equipar 3/roda/Q; testar SEM GRAMPO e limite seis.
- Gaveta de treino: prompt Arrombar apenas com grampo equipado; achar o ângulo e segurar. Pilha aparece depois de destrancar.
- Apito perto da tranca: sopro curto; abrir e observar a dica fraca, sem realçar portas através da parede.
- Forçar ângulo errado até quebrar; pinos anteriores permanecem. Depois de três quebras, fica discretamente mais tolerante. Sem grampos, continuar a história pela chave.
- Cancelar/Esc sem consumo, pausar/F10 e voltar; movimento e câmera retomam ao sair.
- Testar modo assistido, automático (consome um), reduzir efeitos e manter lanterna.
- Salvar/carregar: quantidade, coleta, tranca aberta, pinos parciais e dica usada; save antigo sem campo novo aceito.

## Falas e áudio

63 falas no catálogo editável LockpickDialogue: 44 previstas reaproveitadas, 19 novas. Assets/Docs/Lockpick_Falas.csv, Dublagem/Dublagem_MESTRE.csv/.xlsx, Roteiro_POR_DUBLADOR e Roteiro_Homem1_FUTURO_LOCKPICK.md (nome histórico mantido, conteúdo ativado). 912 IDs na mestra, incluindo históricos. Categorias de anotação aguardam conexão após aprovação dos cinco textos. Nenhuma das 63 novas ligações possui gravação de voz correspondente: legendas provisórias, campos AudioClip prontos, sem TTS.

Gravação nova CC0: Lockpicking Sound, Tegurd, OpenGameArt. Raspar e clique são recortes reais; trava/fecho/clique/quebra representativa reutilizam WAVs locais licenciados. Fontes, hashes e recortes em Assets/Audio/SFX/Lockpick/PROVENANCE.json e Assets/ThirdParty/LICENCAS.txt. O som de quebra é um impacto metálico discreto, não uma gravação literal de grampo quebrando. Opcional de acabamento: procurar “hairpin snap / lock pin break” em OpenGameArt/BigSoundBank CC0; gravar os sussurros e comentários pelos roteiros, nunca TTS.
