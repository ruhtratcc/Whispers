# Capítulo 5 — As Memórias

O pedido “continue”, após a entrega do Capítulo 4, autoriza esta etapa. O capítulo termina junto à escada central, com o título do Capítulo 6. A subida distorcida e o terraço pertencem à próxima etapa e aguardam aprovação.

## O que foi implementado

O Capítulo 4 continua para o 5 na escola já carregada, conservando posição, equipamento, documentos, portas, bateria e sanidade. A investigação usa o Arquivo antigo e a Sala de projetos existentes.

1. Entrar no Arquivo antigo que a chave abriu. Uma lembrança intrusiva apresenta crianças e uma frase incompleta da professora.
2. Reproduzir a fita 1 na primeira bancada do arquivo: “Se alguém encontrar isso, não suba.” e “Ele fica esperando lá em cima.”
3. Encontrar a fita 2 na Sala de projetos, ao fundo do corredor do hospício, do lado oposto ao Depósito técnico: “João prometeu que voltaria.”
4. Voltar ao arquivo e investigar a fotografia na segunda bancada. João reconhece o próprio rosto e lembra que estudou ali, esteve na noite dos desaparecimentos e conseguiu sair.
5. Voltar pela saída do hospício e pelo corredor central até o pé da escada principal. A criança aponta a subida. Ao observá-la, aparece “Você prometeu.” e o capítulo termina.

As memórias mostram silhuetas por até 2,4 segundos, com luz quente própria e deslocamento curto de 0,7 m. São lembranças sem colisão ou AI. Pausa oculta a lembrança e conserva sua duração restante; ao retomar, o efeito continua. A criança guia nunca ataca ou bloqueia o jogador.

Os diálogos e as gravações permanecem em legendas, seguindo a preferência existente por não usar voz gravada. Transcrições e fotografia ficam nas anotações. Há duas reservas de pilhas e um caderno que recupera sanidade; esses objetos só ficam disponíveis neste capítulo. As revelações retiram sanidade uma vez. Blackout retorna ao início do Capítulo 5, preservando os capítulos anteriores.

## Como testar

Use Continuar no menu com um checkpoint escolar e termine o Capítulo 4. Seus cartões passam para o 5 automaticamente. Use **E** para interagir, **F** para a lanterna e **I** para objetivo e anotações. Siga os nomes dos cômodos no objetivo; a criança final fica junto à escada central, antes da subida.

Pause durante um flash e confira que a luz e as silhuetas desaparecem. Ao retomar, confira que o restante do flash acontece. Salve durante uma gravação e continue: a gravação recomeça, a pista não duplica e os choques anteriores não se repetem. No blackout, Tentar novamente restaura o início deste capítulo, incluindo equipamento e sanidade de entrada.

## Integração e limites

Código canônico em `work/ChapterFive`, instalado no projeto Unity existente. Sistemas de interação, narrativa, anotações, save e sanidade reaproveitados. Grupo próprio de objetos: “Escola — capítulo 5 e memórias”. StoryBook próprio: `SchoolStoryThroughChapterFive.asset`. Nenhum download, Meshy, GUI ou reconstrução do mapa. A arquitetura existente e as cenas prontas são preservadas.

Gravadores e fotografia têm arte nativa simples, apoiada nas bancadas existentes e afastada dos monitores. Mobiliário e texturas aprovados continuam presentes. A duração e a intensidade ainda precisam de sessão humana; cabe melhorar a arte dos retratos, o movimento das lembranças e o ritmo de exploração. Não há build de PC ou medição de FPS nesta entrega.

Os testes de pistas usam posições apoiadas e raycast real. A caminhada contínua automatizada cobre apenas um trecho curto do corredor; não prova o percurso inteiro pelo campus.

## Validação final — 08/10/2026

**29 testes PlayMode passaram, zero falhas:** cinco testes compartilhados do Capítulo 5, quatro testes na escola real, três regressões do Capítulo 4 na escola, seis regressões compartilhadas do Capítulo 4, oito de sanidade e três regressões do Capítulo 3. Os quatro testes da escola incluem a passagem real do 4 para o 5, investigação completa e cartões finais, save/load durante a gravação e durante a revelação da fotografia, pausa do flash e blackout com reinício do capítulo atual. O trecho de caminhada contínua usa W por dois metros; as demais etapas usam posições apoiadas para interação por raycast.

Compilação nativa sem erros. A auditoria confirma escola salva e limpa, fora de Play, sanidade 100, luzes elétricas apagadas, pistas/memórias/guia/pilhas em estado inicial, uma câmera, zero inimigos, scripts ausentes ou IDs persistentes duplicados. Os três objetos ficam nas bancadas existentes, com margem de 0,016 m e afastamento lateral de 0,48 m dos centros para evitar os monitores. As entidades têm zero colliders; os sete colliders novos pertencem às pistas, ao volume de entrada, à âncora e às pilhas.

A assinatura física da arquitetura permanece `82E251C5DEE771F623A072CF2D80D100BD795AB4E3524AEDE0C935BBE747EACB`. Menu, escritório, estrada e prefab compartilhado mantêm seus hashes. Checkpoint e preferências reais foram restaurados; o guard dos testes foi encerrado. Onze fontes canônicas e suas cópias instaladas têm hashes idênticos. Três prévias foram renderizadas pelo Unity e inspecionadas: `gravador.png`, `memoria.png` e `escada.png`.

Relatórios finais: `validation.json`, `final-scene.json`, `protected-after.json` e `source-mirrors.json`. A revisão independente do runtime e do builder não encontrou defeitos concretos nos fluxos de persistência, gate do Capítulo 6 ou colisão das entidades. Arte, duração e tensão ainda admitem o polimento descrito acima.
