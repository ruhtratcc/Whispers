# Correção dos cartões — grampo e biblioteca

Corrigido o pedido de 01/11/2026:
- Removidos o botão “Não mostrar mais dicas”, sua confirmação e o código/navegação correspondente. O rodapé agora oferece Entendi e, quando há animação, Ver de novo.
- Reativadas as dicas no perfil atual (Off → Full), sem abrir ou alterar o arquivo de save pessoal.
- Uma configuração explícita atual não é mais sobrescrita pela opção antiga de um checkpoint. Se ainda não existe preferência local, a opção do save continua sendo importada.
- Legendas curtas de som, como [Passos], não bloqueiam o cartão; diálogos e falas continuam tendo prioridade. Foi reproduzida a falha antiga antes do ajuste.
- Cartão do apito ligado à entrada na biblioteca: sinal existente bd.school.library. Não depende mais de ler as anotações. O serviço também reconhece o sinal já concluído ao inicializar, para recuperar uma explicação ainda não descoberta.

Verificação: compilação sem erros; 15 testes PlayMode dos cartões, 18 do apito, 3 do grampo e 348 EditMode aprovados (384 testes únicos). Os dois testes de regressão falharam antes da correção e passaram depois. Teste nativo: pegar o grampo por E abre a explicação após a reação de João; entrar na biblioteca abre a do apito antes de ler o mural. Save/load e ausência do botão verificados. Capturas em quatro proporções de tela aprovadas; duas copiadas para esta entrega.

Arquivos: TutorialCardSystem.cs (fila/contexto/restore), TutorialCardData.cs (preferência explícita), TutorialCardView.cs (remoção do botão), Data/TutorialCards/whistle.asset (gatilho), TutorialCardPlayTests.cs e TutorialCardVisualTests.cs (regressões). Os caminhos de código são relativos a Assets/HorrorGameplay/.

Teste rápido: pegar o primeiro grampo e esperar a fala terminar; depois entrar na biblioteca. Ambos devem abrir sua explicação. Dicas já marcadas como vistas continuam acessíveis em Pausa > Ajuda. A mudança do gatilho não altera o desbloqueio narrativo dos efeitos do apito.

Não verificado: campanha inteira no save pessoal e sensação em mouse/teclado do usuário. Testes executados com perfis descartáveis; nenhum build/ZIP criado.
