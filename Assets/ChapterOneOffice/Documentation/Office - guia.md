# Whispers of Past — Escritório de João de Claus

O cenário do primeiro capítulo está em `Assets/ChapterOneOffice/Scenes/Chapter1Office.unity`. Foi construído diretamente no Unity, com objetos organizados, materiais procedurais e peças de mobiliário com bordas chanfradas. Não foram usados Meshy nem modelos externos.

## Jogar

1. Abra `Assets/HorrorMenu/Scenes/MainMenu.unity`.
2. Aperte Play, selecione **Novo jogo** e depois **O TELEFONE**.
3. Explore o escritório. Também é possível abrir `Chapter1Office.unity` e apertar Play diretamente.

Controles: WASD para andar, mouse para olhar, Shift para correr, Ctrl para agachar, E ou clique para interagir, F para a lanterna, Tab para inventário, Esc para pausa. O pulo segue a opção existente no Player. A lanterna começa desligada no escritório; sua intensidade permanece em 90.

A opção Continuar usa a cena do último checkpoint. Se existir um save anterior do laboratório, Continuar ainda pode abrir TestScene. Use Novo jogo para iniciar o escritório.

## Cenário e objetos

A sala mede aproximadamente 8 × 7,3 metros, com teto de 3,1 metros e um pequeno vestíbulo atrás da porta. A circulação passa pelos dois lados da mesa.

- Mesa de madeira com gaveteiros, puxadores de latão e uma gaveta funcional.
- Telefone preto de disco com números, receptor e fio enrolado.
- Computador CRT, teclado, gabinete, fio no piso e brilho discreto da tela.
- Luminária de mesa, caneca com café, material de escrita, documentos, pastas, porta-retrato e identificação de João.
- Duas estantes com livros, dois arquivos metálicos e caixas de documentos.
- Quadro de investigação com fotos, anotações, alfinetes e fios; mapa emoldurado dos casos.
- Cadeira de João, duas cadeiras de visitantes, tapete, lixeira, cabideiro com casaco, relógio e calendário.
- Janela com persianas e iluminação fria da rua.
- Porta com dobradiça, painéis, vidro fosco, identificação e maçaneta. A porta abre e fecha pelo sistema de interação.
- Dois documentos coletáveis: identificação de João e agenda.

A iluminação combina teto quente, luminária de mesa, monitor e luz fria da janela. O perfil URP e o HUD existentes continuam ativos. A respiração sintética e a ambiência de chiado permanecem desligadas.

## Editar

Na Hierarchy, expanda **Escritório de João de Claus — Capítulo 1**. Os móveis e detalhes possuem nomes próprios e podem ser reposicionados. Mantenha espaço para o CharacterController: raio de 0,32 m, altura em pé de 1,8 m.

Os modelos simples e materiais ficam em:
- `Assets/ChapterOneOffice/Models`
- `Assets/ChapterOneOffice/Materials`
- `Assets/ChapterOneOffice/Textures`

Os colliders principais são separados dos detalhes visuais. Ao editar a mesa, preserve seus colliders e a gaveta. Ao mover a porta, mova o objeto pai **Porta do escritório**, incluindo dobradiça, folha e batentes. Não mova apenas a folha: o pivô determina a animação.

Os componentes `InteractiveDoor`, `InteractiveDrawer` e `CollectibleItem` usam IDs persistentes. Preserve os IDs existentes; ao duplicar um objeto persistente, atribua um ID novo para não compartilhar estado no save.

O gerador fica em `Assets/HorrorGameplay/Editor/OfficeSceneBuilder.cs`. `Create()` se recusa a sobrescrever uma cena existente. A cena pronta é o arquivo principal para alterações. `DetailScene()` adiciona os detalhes complementares com uma proteção contra duplicação; não é necessário executá-lo para jogar.

## Integração com o menu

`Assets/HorrorGameplay/Data/SceneCatalog.asset` contém:
- Main Menu Scene: `MainMenu`
- Gameplay Scene: `Chapter1Office`

O catálogo é compartilhado pelo menu e GameSession. O fallback do MainMenuController também está definido para Chapter1Office. A apresentação do capítulo agora mostra **O TELEFONE**, com a frase “Uma ligação muda o rumo da investigação.”.

MainMenu, Chapter1Office e TestScene constam na lista de cenas da build. TestScene permanece disponível como laboratório para os sistemas.

## Limite desta entrega

Esta entrega cria o cenário explorável. O telefone ainda é um objeto visual: não há ligação, dublagem, sequência de diálogos, coleta de equipamentos obrigatória ou transição para o capítulo 2. O vestíbulo termina após a porta. A janela não contém um mapa externo navegável.

Os documentos são breves elementos de contextualização, sem antecipar a revelação do roteiro. Os demais capítulos e os acontecimentos completos continuam guardados para desenvolvimento posterior.

## Validação

Foram aprovados 5 testes EditMode e 12 testes PlayMode. Dois testes novos exercitam a cena do escritório: colisão com parede e porta fechada, circulação junto à mesa, passagem pela porta aberta, gaveta, coleta e leitura de nota, lanterna, save, pausa e retorno ao menu.

Os testes mantêm cópias do save original e restauram suas configurações de entrada. A entrada virtual é configurada para não ser perdida quando o Editor fica sem foco durante a execução automática. Isso não altera a configuração de controle do jogo.

## Problemas comuns

- **Continuar abre o laboratório:** é um save anterior. Novo jogo abre o escritório.
- **E não mostra interação:** aproxime-se e mire na folha da porta, gaveta funcional ou documento. Os outros detalhes são decorativos.
- **Não consigo mover após ler:** feche a nota ou use a opção Retomar.
- **Lanterna inicialmente apagada:** pressione F.
- **Porta para no meio:** o sistema evita atravessar o jogador. Afaste-se do arco de abertura.
- **Objetos duplicados compartilham save:** dê IDs persistentes diferentes.
- **Materiais rosa após importar em outro projeto:** esta cena depende de URP e dos assets/fontes/sistemas do projeto atual. O ZIP é um backup desta entrega, não um pacote independente de todas as dependências.
