# Inventário e pendências

Auditoria nativa única: 66 assets de dados, 1253 campos AudioClip/SoundSet e 17 AudioSources em quatro cenas. A captura anterior de textos foi reaproveitada com o manifesto de humanização; nenhuma segunda varredura de texto foi necessária. Fonte bruta preservada em work/Dublagem20261029/audit.json. Auditoria_de_campos.csv expõe cada campo, inclusive o que não é voz.

- Livro ativo: 12 falas de narrativa, todas com campo voice vazio.
- Três transcrições de fitas existem, mas estão em modo captionOnlyRecording e sem a voz humana correspondente. Chiado não conta como dublagem.
- Rádio: sete trechos normais, anúncio reutilizado nas duas primeiras repetições, terceira tomada alternativa (P2, fallback existente) e cantiga. Não inventamos um comercial nem outra cidade para preencher o rádio.
- “não responde ela”: existe texto, falta childWarning. Recados de celular e emails são escritos; possíveis leituras são P3, sem fingir slot de voz já existente.
- PC: cantiga sem guia e recado “Helena 2006” sem texto autorado. Três linhas/stems BLOQUEADOS na planilha não são falas para ler literalmente. Datas da fita de 2007 não corrigem esse buraco.
- Coro Recusar: quatro stems EXISTE/P1, um slot refusalCry. Vozes individuais precisam ser montadas, não sobrescritas uma sobre a outra.
- Respirações de esforço/ligação: três usos já ligados a efeitos vocais licenciados. Não são takes do elenco; mantidos e sem exigir nova gravação. Apito real já existe: não fazer assobio de boca.
- Não há fala automática da figura infantil, carteiras ocupadas ou reflexo sorrindo. São eventos visuais/efeitos. As opções reativas/vocais são propostas com gate de percepção; não inventam cena nem um perseguidor.
- Telefone antigo do escritório/Aurélio/policial e roteiros antigos estão em Legado_NAO_GRAVAR: 52 textos distintos arquivados/compatibilidade. Não reativar a campanha anterior, nem contar essas vozes como prontas para a atual.
- UI, teclas, botões, objetivos e nomes de janelas não são narração obrigatória. Apenas pensamentos textuais atuais identificados estão no Mestre como EXISTE/P2, e leituras de documentos como EXISTE/P3. Textos de foto vazios foram excluídos; não se grava uma linha em branco.

## Conferências e limites

IDs de produção são novos nomes de arquivo neste documento; nenhum ID de história foi renomeado no jogo. Texto idêntico do mesmo personagem reutiliza uma linha e vários vínculos; terceira repetição do locutor tem ID próprio por ser outra performance. O número de usos reativos difere do de gravações únicas. Voz infantil sem identidade não é atribuída factualmente à Marta só porque usa a mesma atriz.

Os quatro alunos continuam Marta/Bia/Caio/Luan, sem entidades que ataquem ou persigam. Dicas exigem pista conhecida; nenhum bark deve servir para criar o evento que descreve. Condições adicionais de percepção, reprodução anterior, recarga e variante são obrigatórias. Todas as novas falas são opcionais; duração estimada não obriga falar correndo.

Nenhum áudio baixado/gerado, nenhum TTS e nenhum efeito aplicado. Importador omitido por segurança do modo caption-only e das montagens; sistema reativo só documentado. Nenhum ZIP. Save pessoal não acessado.

NÃO VERIFICADO: duração de takes ainda ausentes, qualidade da atuação, saúde/conforto individual, sincronização percebida, encaixe de animação, emendas e mixagem em hardware. Sem mudança de código de jogo: não há compilação nova exigida nem testes Play reexecutados neste passe documental. A auditoria executou C# efêmero no Editor; a integridade dos arquivos é conferida por SHA-256.
