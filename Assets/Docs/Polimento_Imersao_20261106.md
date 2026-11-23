# Polimento — 06/11/2026

Aplicado diretamente no projeto C:/Users/ruhtra/My project. FOV 49, IDs/flags e save pessoal preservados; testes com diretórios Temp. Sem ZIP, TTS ou nova geração ElevenLabs.

## Alterações
- Espelho: golpe da lanterna em primeira pessoa, com preparação e avanço até o vidro. Vidro e abertura mudam no impacto; o som existente mirror_break toca nesse ponto com ganho maior. Pausa suspende o gesto; restauração cancela a apresentação sem repetir a quebra. Reflexo recortado dentro do espelho preservado.
- Ratos: modelo pronto animado CC0 de br-n518 (OpenGameArt), com textura, corrida rápida e seis trajetos de canto na secretaria/biblioteca. Sons locais de patas acompanham a posição do rato em 3D; guincho ocasional. Não atacam nem bloqueiam o jogador. Material escurecido e escala conferida em renderização.
- Pegadas: interação analisa o chão, sem levantar o rastro na inspeção 3D; registra a pista e mantém bd.school.cups e a chave/gaveta. Quatro solas originais diferentes, variação de tamanho/ângulo/posição e quatro intensidades de poeira em cada trilha. O pulso do apito conserva essas diferenças.
- Biblioteca: quatro fotos reais do acervo ocultadas; doze documentos/recortes reposicionados inteiramente dentro do painel. IDs e interações mantidos.
- Objetivo do espelho: “Saia da sala da fita e siga até a última porta à direita, no fim do corredor.” Texto da história e catálogo de objetivos sincronizados.
- Carro: olhos do motorista alinhados ao volante e aproximados; caixa da lanterna com escala uniforme, lanterna apoiada no topo. Carro deslocado para a pista esquerda.
- Rua/chegada: 321 marcações antigas/laterais desativadas, nova faixa central tracejada em uma malha alinhada ao eixo da rua. Passeio separado do asfalto em altura; antes ambos terminavam exatamente em y=0, causando sobreposição visual.
- Celular: foto da secretaria recapturada com vão aberto; GUID da imagem preservado. Onze mensagens secundárias passaram a chegar somente ao consultar o aparelho, reduzindo interrupções. Nenhum ID/texto/pista foi removido; mensagens necessárias à progressão continuam automáticas.
- F8: terminal com comando skip, sem execução de código/shell. Disponível nas três cenas; skip funciona somente no carro. Usa os sinais normais e entrega os equipamentos antes de carregar a escola. Comandos desconhecidos não mudam a partida.
- Escritório: telefone girado para o lado do usuário; raiz da interação mantida.

## Testes reais
16 testes únicos PASS: 4 Editor + 12 Play. Incluem F8/skip até SchoolCEEP com equipamento e checkpoint descartável; análise das pegadas, quatro texturas/níveis de transparência durante o pulso, rato se movendo com animação e áudio 3D, pausa e save/load; golpe do espelho, escolhas, restauração e final com TV/email do escritório. Ver testes-finais.json.

Preservação comparada com backups: 180 IDs da escola, 1 da estrada, 9 do escritório; IDs de mensagens e marcos da história intactos. Foto alterada com mesmo GUID. Painel: zero renderizadores ativos ultrapassando suas bordas na auditoria. A primeira rodada teve duas falhas de ferramenta/isolamento (timeout do comando de autotick durante o teste e fixture seguinte com resíduo); registros brutos estão em work. Rodadas finais isoladas passaram sem ignorar esses erros.

## Arquivos principais
Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionMirrorFlow.cs — sincronização do golpe e quebra.
Assets/HorrorGameplay/Scripts/Narrative/SecretaryEvidence.cs — pulso preserva materiais individuais.
Assets/HorrorGameplay/Scripts/Inspection/InspectableItem.cs — análise no chão e nota.
Assets/HorrorGameplay/Scripts/Systems/RatScurry.cs — modelo móvel, animação e som espacial.
Assets/HorrorGameplay/Scripts/Systems/SchoolAmbientEvents.cs — aciona corridas nos cantos.
Assets/HorrorGameplay/Scripts/UI/ChapterCommandTerminal.cs — F8 e comando skip.
Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionRoadFlow.cs — skip preservando progressão.
Assets/HorrorGameplay/Data/Immersion20261106/ — quatro solas, malhas/materiais de rastros e faixa central.
Assets/ThirdParty/Models/Environment/RatCC0/ — modelo, textura, material e prefab do rato.
Assets/ThirdParty/Images/foto_porta_secretaria.png — foto atualizada.
SchoolCEEP.unity, RoadJourney.unity, Chapter1Office.unity — poses, referências e props.
Assets/HorrorGameplay/Tests/*/ImmersionPolish*Tests.cs — cobertura nova; fixtures antigas ajustadas à análise no chão e quebra no impacto.
Assets/ThirdParty/LICENCAS.txt — licença do rato.

Backups anteriores às alterações em Assets/_Substituidos/ImmersionFix20261106/. Ferramentas externas em work/ImmersionFix20261106/; não relançar instaladores.

## Ajustes disponíveis
RatScurry: speed=3.4 m/s, reach=16 m; routes, paws e squeaks na raiz GameplayRoot. SchoolAmbientConfig: intervalo 24–48 s. Mirror: curva do golpe .58 s em StrikeWithFlashlight; SoundSet mirror_break no componente. Terminal: F8 abre/fecha; skip + Enter durante o carro.

## NÃO VERIFICADO
Escuta humana do mix/volume, campanha inteira jogada manualmente, sensação do enquadramento do motorista e do golpe em todos os ângulos, executável/standalone. Renderizações diagnósticas conferidas: rato e foto da secretaria; não são prova de uma campanha inteira nem de áudio ouvido por uma pessoa.

Estado final verificado: SchoolCEEP salva, Play parado, compilação sem falhas, 0 scripts ausentes, 180 IDs únicos, FOV 49, TestSaveDirectory nulo e nenhuma escrita pendente. workspace.json.restored confirma a restauração do editor.
