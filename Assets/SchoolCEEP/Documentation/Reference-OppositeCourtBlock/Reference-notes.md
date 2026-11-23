# Referências do acesso entre blocos após o corredor principal

Referências recebidas em 04/10/2026; registro atualizado em 05/10/2026. Nove fotos preservadas, com a localização explicada pelo usuário e o escopo aplicado ao acesso entre os blocos oeste. A validação da reconstrução atual será registrada a partir dos relatórios nativos ao fechar a revisão.

## Localização confirmada pelo usuário

O conjunto fica depois do corredor principal comprido, no lado oposto à quadra. A escada curta e a rampa ligam áreas em níveis diferentes: seguindo para a esquerda na parte alta chega-se a um bloco; descendo chega-se a outro bloco.

Resposta original do usuário:

> a parte da rampa e escada e a seguir dps do corredorzao dai pra esquerda na parte alta seria um bloco e descnedo seria outro bloco

A resposta confirma essa relação de circulação, mas não identifica nominalmente os dois blocos nem confirma que as duas fotos da fachada alta pertencem ao mesmo prédio das outras quatro.

Nas três fotos enviadas posteriormente, o usuário identificou explicitamente as duas primeiras como o bloco da parte alta e a última como o bloco inferior. Essa associação das fotos novas está confirmada; não resolve por si só a correspondência das fachadas das fotos iniciais 1–2 nem os nomes e níveis dos blocos na planta.

## Fotos preservadas

| Arquivo local | Anexo original | Conteúdo visível |
| --- | --- | --- |
| [Reference-1.png](Reference-1.png) | codex-clipboard-3edc087b-209d-4741-a9ac-14ed7e155c4e.png | Fachada alta branca e azul, janelas superiores azuis, grades nas janelas térreas, banco azul, jardim e entrada de corredor sob cobertura. |
| [Reference-2.png](Reference-2.png) | codex-clipboard-75e9591f-7db4-4298-b6df-45d16ec5f152.png | Vista lateral da fachada alta, janelas térreas com grades e tela, pilares azuis, caminho de concreto e canteiros. |
| [Reference-3.png](Reference-3.png) | codex-clipboard-50cce176-7dc4-43c5-a1fb-e44d2ee2d8ef.png | Escada curta, corrimãos azuis, acesso superior à direita e placa amarela legível “BLOCO 3”. |
| [Reference-4.png](Reference-4.png) | codex-clipboard-704b5731-57e4-4378-9d36-0368fee48e92.png | Rampa externa com trechos paralelos, patamar junto à parede azul e corrimãos azuis fixados ao piso. |
| [Reference-5.png](Reference-5.png) | codex-clipboard-b6c4ab57-2215-4fb2-866c-558c22e00cc7.png | Vista oposta da rampa, corrimãos e cobertura baixa de chapa metálica. |
| [Reference-6.png](Reference-6.png) | codex-clipboard-75a58d4d-dbb8-4bba-97b5-6153b4030630.png | Os três trechos paralelos da rampa e sua cobertura baixa, com pilares e vigas metálicas. |

## Fotos posteriores: bloco alto e bloco inferior

| Arquivo local | Anexo original | Associação confirmada e conteúdo visível |
| --- | --- | --- |
| [Upper-block-1.png](Upper-block-1.png) | codex-clipboard-8f000685-94dd-4e07-80c4-ec160b37bc27.png | Bloco da parte alta. Entrada com portas metálicas e bandeira superior de vidro; corredor interno com paredes creme/amarelo claro na parte inferior, branco acima, piso cerâmico claro, portas dos dois lados e luminárias pequenas na parede. |
| [Upper-block-2.png](Upper-block-2.png) | codex-clipboard-0f531756-9773-49f7-a888-a16c55893471.png | Bloco da parte alta. Corredor interno com portas escuras dos dois lados, bandeiras de vidro sobre as portas, paredes de duas cores, piso cerâmico claro, teto de madeira entre travessas claras e luminárias ovais na parede. |
| [Lower-block-1.png](Lower-block-1.png) | codex-clipboard-83c2de7f-b761-4282-8fd0-b574ee79b7aa.png | Bloco inferior. Circulação externa junto a fachada de revestimento cerâmico amarelo, pilares creme/castanho, janelas altas escuras, beiral e forro de madeira, banco, portão gradeado e jardim ao lado do caminho. |

Os acabamentos das três fotos posteriores foram associados ao corredor interno do bloco alto e ao corredor externo do bloco inferior. A aplicação utiliza os identificadores prováveis `west_south` e `west_north`, respectivamente. O corredor central e as salas do segundo andar mantêm suas referências próprias.

## Leitura das referências

As fotos 3–6 mostram o mesmo conjunto de escada curta e rampa externa. A identificação “BLOCO 3” está diretamente legível na foto 3; essa placa não determina sozinha o nome do bloco inferior.

O conjunto apresenta três trechos de rampa paralelos, corrimãos de aço pintados de azul, patamares de concreto e uma cobertura metálica baixa com inclinação única. A cobertura acompanha uma área externa entre acessos; não é o telhado alto triangular da rampa central.

Esta referência deve permanecer separada de `central_south_ramp`, que tem quatro trechos paralelos e cobertura alta em duas águas. Também não deve ser tratada como uma troca de acabamento da rampa central já existente.

## Associação provável com a planta

Em `work/SchoolCEEP/plan-landmarks.json`, o conjunto que melhor corresponde às fotos 3–6 é `west_north_ramp`, com três trechos horizontais em zigue-zague, junto de `west_north_short_steps`, imediatamente a leste. Isso é uma associação por forma e posição na planta, ainda não uma identificação de nomes ou cotas confirmada pelo usuário.

As caixas em coordenadas do PDF são `[351.88, 756.8, 505.0, 820.64]` para a rampa e `[505.0, 794.36, 553.12, 829.16]` para a escada curta. A planta registra inclinação de 8,33%, mas não fornece os níveis dos extremos desse acesso. Não usar o desnível de 3,81 m da rampa central para este conjunto.

A correspondência exata das fachadas brancas e azuis das fotos 1–2 permanece em aberto. Seus acabamentos não foram aplicados aos blocos oeste. A relação usada na aplicação é: corredor principal → acesso de escada/rampa → bloco à esquerda no patamar alto (`west_south`) e outro bloco no nível inferior (`west_north`). A relação alto/baixo foi confirmada pelo usuário; os identificadores são interpretação da planta.

## Geometria e acabamentos aplicados

As fotos iniciais 3–6 orientam a ligação externa: três lances de rampa paralelos com dois retornos, escada curta de oito espelhos, patamares e plataforma superior, corrimãos azuis fixados ao piso e cobertura metálica baixa com uma água. O acesso conecta a passarela comprida, o corredor inferior e a entrada do corredor alto.

O bloco alto foi colocado a 1,50 m acima do bloco inferior. Essa é uma estimativa derivada dos três lances de aproximadamente 6 m e da indicação de 8,33% da planta, compatível com a escada fotografada. O usuário não informou a cota vertical. A aplicação conserva a distinção entre esse desnível local e os pavimentos do prédio central.

No corredor alto, foram integrados pintura creme embaixo e branca acima, piso claro com juntas de aproximadamente 30 cm, portas nativas castanho-escuras com bandeiras de vidro, forro de madeira entre travessas claras e luminárias pequenas ovais nas paredes. O corredor permanece entre duas fileiras de salas.

No corredor inferior externo, foram integrados revestimento amarelo com malha de aproximadamente 15 cm, parede branca acima, janelas altas com seus vãos reais, acabamentos castanho/creme em pilares, beiral com forro de madeira, banco junto à parede e canteiros fora da faixa de circulação. Alturas, cores e proporções desses acabamentos são aproximações das fotos.

O acabamento pinta somente as faces nativas voltadas ao corredor. A aplicação não acrescenta colisores decorativos e mantém os vãos, as folhas de porta e as interações existentes. A elevação do bloco alto acompanha seus objetos e mobiliário associados. A reconstrução também inclui a correção da linha simples do limite da passarela oeste que havia sido interpretada como alvenaria.

O agrupamento das superfícies passa a preservar renderizadores com vários materiais. Isso é necessário para conservar as faces creme/amarelas e brancas separadas no acabamento nativo.

## Estado deste registro

Geometria e acabamentos das sete referências identificadas de acesso/bloco alto/bloco inferior integrados às fontes e aplicados em reconstrução da cena. As duas fachadas brancas e azuis permanecem sem correspondência exata e sem aplicação neste escopo. A revisão mais recente está em reconstrução e validação; este registro não declara aprovação física, de circulação ou de reabertura.

O fechamento deve consultar os relatórios da reconstrução atual em `outputs`: `School-west-block-access-report.json` descreve a geometria, `School-opposite-block-photo-report.json` descreve o acabamento e as verificações de preservação, `School-west-block-access-proof.json` registra as provas físicas do acesso e `School-whole-map-audit.json` registra a circulação do mapa. As capturas `School-opposite-block-access-layout.png`, `School-opposite-block-high-layout.png` e `School-opposite-block-low-layout.png` apoiam a revisão visual. Os resultados finais serão acrescentados após salvar e reabrir a cena.

## Fechamento verificado — 05/10/2026

Cena SchoolCEEP salva e reaberta, com 399/399 rotas gerais e 881 verificações em 34 caminhos aprovadas. A prova do acesso oeste confirma três lances, oito degraus, 10 rotas e 19 pontos. O desnível local de 1,50 m continua estimado; nomes dos blocos e duas fachadas brancas/azuis permanecem sem associação exata confirmada.

As capturas nativas do acesso, do corredor alto e do corredor baixo foram inspecionadas após a reconstrução final. Os planos de pintura estão completos e sem as diagonais incorretas. SchoolOppositeBlockPhotoPass revisão3 salva cada conjunto de malhas em novos assets nativos; evita reutilizar o buffer anterior pelo CopySerialized. Geometria, faces, materiais e colisores são os mesmos da revisão anterior. Os renderizadores com múltiplos materiais continuam fora do agrupamento que usava somente o primeiro material.

Os pedidos anteriores também foram conferidos na mesma cena: Automotiva, refeitório e ligação, 20 paredes falsas da planta colorida, corredores centrais/superior, escada e mini parede, portas e muros da rampa nos dois lados, fecho lateral, 22 salas e saídas metálicas somente em cima. Todas as poses temporárias de portas foram restauradas. As provas são físicas/geométricas no Editor e não executam gameplay ou checkpoints.

Prévias diurnas servem à leitura da arquitetura; a cena conserva noite e névoa. Uma câmera, nenhum objeto de captura e portão da oficina fechado. Evidências e quinze imagens atuais relacionadas em School-current-completion.json e copiadas para Assets/SchoolCEEP/Documentation.
