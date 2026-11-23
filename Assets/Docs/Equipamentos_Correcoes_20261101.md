# HUD e uso pelo mouse — 01/11/2026

- HUD inteira oculta até a lanterna estar disponível: não aparece no escritório nem na estrada antes da coleta, mesmo com apito no inventário. A nova regra substitui o slot desativado do escritório.
- Ícones, nomes e números vinculados às definições de equipamento em Awake/OnEnable. Diagnóstico na cena reaberta: os dados das definições estavam corretos, mas Images tinham sprite nulo e textos dos slots mostravam 0. A HUD agora recupera esses dados ao carregar; um ícone ausente também é ocultado para não formar retângulo branco.
- Removidos ACESA/APAGADA da HUD. Avisos de bateria fraca/sem pilha e a barrinha continuam.
- Clique esquerdo usa o item equipado: lanterna liga/desliga; apito toca ou sustenta o sopro enquanto segura. F e botão do meio continuam como alternativas. E continua interagindo com o cenário; o mesmo clique não usa o equipamento e abre uma porta/gaveta. No carro, com o equipamento fora da mão, o clique continua interagindo normalmente.

Compilação real sem erros e **33 testes únicos passaram**: 15 de equipamento, incluindo cenas reais, mouse simulado no Input System, coleta, PC/celular, save legado e renderização em quatro proporções; 18 regressões do apito. Novo teste confirmou imagens, nomes e números após carregar a escola salva. A primeira tentativa de captura falhou porque o Canvas da câmera de teste estava fora da camada renderizada; corrigido somente o teste, e a repetição passou.

Captura revisada: hud-from-saved-school.png, uma renderização isolada da HUD carregada da escola, sem o cenário. Os demais hud-*.png cobrem 16:9, 16:10, 4:3 e ultrawide.

Arquivos alterados: EquipmentController.cs, EquipmentHud.cs, InputReader.cs, WhistleSystem.cs, PlayerInteraction.cs e EquipmentPlayTests.cs, todos sob Assets/HorrorGameplay. Não houve novos downloads, mudança de ID/schema de save ou edição das cenas. Perfil pessoal não utilizado; testes em diretórios descartáveis. Sem ZIP.

Como conferir: iniciar no escritório/estrada e verificar ausência da HUD; coletar a lanterna; clicar para ligar/desligar; equipar apito com 2 ou Q e clicar/segurar; usar E diante de uma gaveta e conferir que soprar não a abre; pausar/abrir PC/celular e verificar bloqueio do equipamento.

NÃO VERIFICADO: sensação humana do mouse, áudio no headset e campanha inteira jogada manualmente.
