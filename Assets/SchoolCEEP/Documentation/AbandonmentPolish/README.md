# Polimento de imersão — 10/10/2026

Aplicado à SchoolCEEP existente em `C:/Users/ruhtra/My project`. Menu, escritório, chegada, roteiro e progressão dos sete capítulos preservados. Não há perseguição ou ataque de entidade. O usuário autorizou downloads sem nova confirmação, priorizando Poly Haven, e rejeitou o modelo Little girl; ele não foi integrado.

## Mudanças

- 46 instâncias de modelos CC0 Poly Haven: 11 relógios com volume, 27 ventiladores parados, 5 lixeiras enferrujadas e 3 vassouras. Prefabs, materiais e dois níveis de LOD nativos compartilhados; sem importador em runtime. Fontes, autores e adaptações em CREDITS.md.
- 82 renderers de alvenaria / 10.469 triângulos verticais receberam reboco com mofo. Cores antigas e demais faces dos meshes foram preservadas. Repetição PBR calibrada em aproximadamente 2,77 m; relevo e oclusão suaves.
- 21 caixas adicionais apoiadas nas bordas da circulação, com colliders ajustados. A caixa 11 foi removida após o auditor detectar obstrução na passagem baixa da cantina. Reservas agora incluem os 422 trajetos encontrados e 40 trajetos explícitos de escadas/conectores; 34.368 amostras sem contato com as caixas restantes.
- Cantina: oito materiais próprios sem reflexo artificial do céu. Vidros, metais e faixa azul do campus revisados: 36 variantes, 3.074 renderers, 3.098 slots, incluindo 19 slots de vidro. Pintura azul de alvenaria separada de caixilhos em 24 renderers / 1.238 triângulos; pintura fosca e menos saturada. Pistas e aparições narrativas protegidas, incluindo a janela final intencionalmente iluminada.
- Lanterna da escola: sombras locais corrigidas; brilho diminui suavemente ao inspecionar superfícies próximas e recupera ao apontar para longe. Alcance 17 m e intensidade base 90 mantidos; pausa, bateria, alternância e efeitos de sanidade preservados. Opt-in da escola, desativado por padrão nos outros cenários. Near plane persistido pelo Unity é 0,1 m.
- Névoa no ambiente e filtro de imagem da etapa anterior mantidos; não foram acrescentadas partículas presas à câmera.

## Verificação

`flashlight-regression.json`: um teste PlayMode aprovado, cobrindo exposição próxima/distante, consumo sem recarga artificial, pausa e alternância.

`opening-regression-after-immersion.json`: um teste integrado aprovado, cobrindo portão, pistas por raycast, visibilidade, progressão inicial, sanidade, blackout e save. Usa deslocamentos de teste entre locais; não comprova caminhada humana contínua pelo campus inteiro.

`map-audit-final.json`: 422/422 trajetos encontrados, zero destinos inacessíveis e zero juntas de parede suspeitas. A passagem baixa da cantina passa agora na amostragem e no CharacterController.Move em ambos os sentidos. O relatório geral continua `passed=false`: seis asserções do contrato antigo de gameplay e uma falha do percurso sintético na folha esquerda da entrada. Não declarar esse auditor inteiro aprovado. A subida da entrada copiada foi testada com o motor real no refinamento anterior (`outputs/Refinement/tests-road.json`); isso não transforma a falha sintética em resultado aprovado.

`integration-final.json`: arquitetura física anterior preservada, props apoiados, reservas livres, geometria e UVs dos materiais preservados e segunda aplicação idempotente conferida. O hash bruto de MonoBehaviours mudou apenas pelas configurações autorizadas da lanterna; a normalização estrita dessas configurações reproduz o hash original. Nenhuma mudança de narrativa foi ocultada pela comparação.

`final-SchoolCEEP.json`: cena salva/limpa e fora de Play, uma câmera/jogador/sessão, zero componentes, meshes ou materiais ausentes, erros de shader ou conteúdo narrativo e zero inimigos.

Os relatórios anteriores e imagens com `diagnostic` são históricos. Prévias finais: `classroom-final-*`, `classroom-clock-final-*`, `classroom-facade-final-*` e `classroom-canteen-final-*`. São renders da câmera real com configurações da lanterna e filtro; não são um playthrough humano. As prévias de materiais antecedem apenas a retirada da caixa da passagem, sem mudança dos materiais.

## Como testar

1. Abra SchoolCEEP e entre em Play. Com F, compare vidros e a faixa azul com a lanterna desligada/ligada.
2. Aproxime-se de uma parede ou relógio e aponte depois para o fundo do corredor: o brilho deve reduzir de perto e recuperar à distância, sem alterar a bateria indevidamente.
3. Passe pela ligação baixa da cantina e pelas caixas dos corredores; portas e trajetos devem permanecer livres.
4. Pause durante o ajuste da lanterna, retome e continue as pistas da escola; confira sanidade e blackout/retry.

Fontes canônicas: `work/AbandonmentPolish/Editor`, `work/AudioPolish/Scripts/Player/FlashlightController.cs`, `work/AbandonmentPolish/Tests/FlashlightImmersionPlayTests.cs` e `work/SchoolAssets/Editor/SchoolBatchVisualReplacement.cs`. Nove fontes instaladas conferidas por SHA256. Passes idempotentes e backups Before preservados; não reconstruir o mapa com um builder antigo sem reaplicar deliberadamente o refinamento.

Esta entrega é polimento do protótipo. Não inclui build de PC, medição de FPS ou avaliação humana da campanha inteira.
