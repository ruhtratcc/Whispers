# Equipamentos — entrega (31/10/2026)

Implementado nas três cenas existentes, com HUD no **canto inferior direito**, conforme a última imagem. Silhuetas e layout originais: linha fina, losangos, número, ícone maior no item ativo, nome em caixa alta espaçada, estado discreto e quatro segmentos de bateria. Fonte Barlow Condensed em atlas SDF 1024, tamanhos inteiros; ícones Full Rect sem compressão/mipmaps. Nenhuma alteração ao desktop, render texture, fontes ou nitidez do PC, celular ou sistema de legendas.

## Teclas e comportamento

| Controle | Ação |
|---|---|
| 1 / numérico 1 | Equipar/guardar a lanterna |
| 2 / numérico 2 | Equipar/guardar o apito |
| 3 / numérico 3 | Slot reservado; ignorado enquanto o item não existe |
| Q | Atalho antigo do apito, mantido; equipa/guarda o slot 2 |
| X | Último equipamento; Q já tinha uma função no projeto |
| Roda | Alternar itens possuídos; sentido invertível nas opções |
| F | Ligar/desligar somente a lanterna equipada |
| Botão do meio | Apito: toque ou segurar para soprar, como antes |

Troca de 0,32 s, com guardar/sacar e uso bloqueado durante a animação. Um item por vez; apito sobe à boca ao soprar. A lanterna lembra seu interruptor, apaga ao ser guardada e só consome pela lógica existente enquanto efetivamente acesa. A opção **Manter a lanterna acesa ao trocar** está desligada por padrão; ligada, deixa o modelo à esquerda com outro item e continua consumindo bateria. Mãos vazias guardam ambos.

Bloqueios: sopro em andamento, menus/pausa, cutscene, inspeção, PC sentado, celular, objeto carregado, esconderijo, carro enquanto sentado e movimento da gaveta/porta usada pelo jogador. A roda não rouba a rolagem de UI. As ações usam a cópia existente do Input System; padrões novos em InputReader, sem editar o asset original de input. O projeto não tinha uma tela geral de remapeamento; nenhuma foi inventada.

## Opções e ajustes

Em **Opções → Equipamentos**: Sempre mostrar / Esconder sozinha (padrão) / Não mostrar; manter lanterna; inverter roda. As opções também são acessíveis na integração existente de opções/acessibilidade do menu. Esc fecha primeiro esse painel na pausa.

Inspector, filho **Equipamentos — canto inferior direito**: rightMargin=42, bottomMargin=72, verticalPosition=0, hideSeconds=2.5, ice/accent. EquipmentController: switchSeconds=0.32 e loweredOffset. Há fade e deslocamento de 10 px na seleção; reduzir efeitos remove o deslocamento e encurta transições. A HUD respeita a área segura e some em modais, falas/legendas e notificações de celular. Novos itens mantêm a HUD por 3,5 s. Não há tutorial de teclas novo no jogo; a antiga notificação Q do apito é suprimida quando este sistema está instalado.

No escritório, a lanterna aparece como slot desativado **NÃO AQUI**, mesmo antes da coleta no carro, para explicar a restrição pedida. O apito continua comum antes da descoberta na escola. Na estrada, a coleta seleciona a lanterna, preserva a saída pela porta e os gatilhos da história.

## Save e compatibilidade

Campo opcional equipment no schema 2 existente, sem mudar IDs/flags: seleção, anterior, interruptor lembrado e preferências. Um marcador initialized distingue o estado novo, pois JsonUtility materializa classes ausentes até em JSON antigo. Save sem o marcador: lanterna apenas se o antigo flashlightOn indica que estava sendo usada e ela está disponível; caso contrário, mãos vazias. Item desconhecido/indisponível é guardado com segurança. Captura, checkpoint, carry e Continuar preservam o estado pela infraestrutura existente. Perfil pessoal não utilizado; testes descartáveis.

## Adicionar um grampo em três passos

1. Em Create → Whispers → Equipment item, definir ID próprio, slot 3, tipo Other, nome, ícone, inventoryItemId e prefab de viewmodel opcional. A definição GRAMPO já existe com inventoryItemId `lockpick`, sem criar esse item no inventário.
2. Incluir a definição em items do EquipmentController nas cenas desejadas. A HUD usa a mesma ordem/lista; atualmente as três definições já estão nas três cenas.
3. Entregar o item pelo Inventory existente usando exatamente inventoryItemId. O slot aparece e pode ser selecionado. O comportamento de abrir fechaduras pertence à futura mecânica de lockpick, não foi implementado neste pedido.

## Validação real

**229 testes únicos passaram**: 12 da integração nova, 197 de saves e 20 regressões do apito (18 isolados, escritório e estrada reais). Incluem teclado principal/numérico, Q/X/F, roda real, mesma tecla guarda, estados bloqueados, gaveta real, dois modos da lanterna, consumo, slot oculto, save/load, JSON antigo, opções reais, PC/celular reais e coleta no carro. A compilação final terminou sem erros.

Renderização real do Canvas em Play, com imagens salvas em 1920×1080, 1440×900, 1024×768 e 2560×1080; limites dentro da tela e pixels de UI conferidos. A captura 1920×1080 foi inspecionada visualmente: é uma cena de teste da HUD, não uma foto de campanha jogada integralmente.

Medição de 64 trocas aquecidas: **0 bytes gerenciados por troca**, tanto isolada quanto na escola real com HUD e sons gravados via pool SFX/UI. Escopo síncrono RequestSlot+Advance; exclui renderização, mixer de áudio e I/O de save diferido. Não há busca global de objetos em Update nos componentes novos; a busca do painel só ocorre ao fechar pelo Esc.

Ocorrências corrigidas: caminho `<Keyboard>/1` (não digit1); marcador explícito para JSON legado; teste de auto-hide considerava incorretamente o destaque inicial de 3,5 s como 2,5 s. Uma resposta de ferramenta expirou ao instalar a escola, mas o marcador installed.json confirmou término; não houve reinstalação duplicada. Quedas pontuais de conexão nas ferramentas foram verificadas antes de repetir comandos.

## Assets e pendências

Baixados somente Barlow Condensed Medium/Regular (OFL) e Fabric Rustling de Iochi Glaucus (CC0). Cinco Foley curtos reutilizam gravações locais; o sexto é o tecido novo. Tudo passa pelo AudioManager existente nos grupos SFX/UI. Três ícones são originais. Licenças em Assets/ThirdParty/LICENCAS.txt, CREDITS.md e OFL.txt; proveniência em JSON.

Não encontrei uma gravação existente da fala de lanterna bloqueada no escritório. Há legenda provisória **“A lanterna ficou no carro.”** e campo officeBlockedVoice pronto para a voz humana. Falta essa gravação; não foi usado TTS. Direção: comentário curto, natural, baixo, sem susto. Nenhum Foley ficou como placeholder silencioso.

**NÃO VERIFICADO:** sensação humana da animação/roda, mix no headset, campanha humana integral em todos os capítulos, todos os monitores físicos/áreas seguras e build standalone deste passe. Não foi feito ZIP. A função de desaceleração/consumo da bateria e os finais não foram refeitos.

## Checklist de Play

- Escritório: slot 1 NÃO AQUI, coletar apito, PC/celular/inspeção escondem HUD e bloqueiam troca; opções/pausa funcionam.
- Estrada: sem slot de lanterna antes da coleta, pegar após estacionar, sair pela porta; só então usar/trocar equipamentos.
- Capítulo 3: apito comum antes da pista; ler a pista e conferir efeito depois; verificar ausência de portas no destaque.
- Capítulo 4: procurar pistas com lanterna, trocar no escuro, verificar bateria baixa/sem pilha e recarregar; testar cooldown e guardar.
- Capítulo 5: troca não permite pular as vozes/portas; verificar direção do eco e da trilha.
- Capítulo 6/finais: não trocar durante sopro do espelho/diálogo; HUD some nas escolhas/tela final; salvar e Continuar mantém o item.
- Todos: 1/2/numérico, Q/X, roda e sentido invertido; repetir com luz à esquerda ligada e desligada; testar as três opções da HUD.

Arquivos por caminho em ARQUIVOS.txt. Evidências: test-runs.json, test-summary.json, installed.json, allocation.json, native-allocation.json e hud-*.png.
