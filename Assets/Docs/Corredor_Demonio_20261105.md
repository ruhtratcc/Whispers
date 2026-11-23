# Corredor C5 e demônio em camadas — 05/11/2026

Implementação integrada à SchoolCEEP (nome interno da cena mantido; escola SEEPE). Sem perseguição, ataque ou pessoas visíveis no corredor. IDs, revisão narrativa e flags existentes preservados. Save pessoal não foi acessado; testes usam perfil descartável. Originais em Assets/_Substituidos/CorridorDemon20261105/.

## Arquivos

- Scripts/Narrative/CorridorAtmosphere.cs e CorridorAtmosphereConfig.cs: renderização modular, névoa, luzes, marcas e restauração do ambiente.
- Scripts/Audio/DemonVoice.cs e DemonVoiceConfig.cs: duas fontes 3D, atraso, fallback no mixer, coro e legenda única.
- BeforeDemolitionTwoVoicesFlow.cs: wrap exato nos dois sentidos, orientação preservada e contador opcional compatível com saves v1.
- FirstPersonMotor.cs: WrapTo reposiciona sem cancelar movimento e suavização do mouse; Warp existente permanece.
- NarrativeDirector.cs e StoryBook.cs: integração dos takes sem alterar a duração das falas antigas.
- Data/CorridorDemon20261105/: configurações, malhas derivadas e 24 materiais.
- Audio/Gameplay.mixer: Voice_Demon_Mask e Voice_Demon_Low, mantendo os grupos anteriores.
- SchoolCEEP/Scenes/SchoolCEEP.unity: cinco módulos visuais idênticos; portas centrais reais preservadas.
- ThirdParty/Images/Corridor/: 24 variações de marcas e mapas normais originais. A galeria da lancheira da Marta permanece.
- Docs/Falas_Novas_Demonio.csv e Demonio_Gravacao_Pos_Producao.md: 32 falas em dois takes mais sessão de sete vozes do coro.
- Tests/EditMode/CorridorDemonTests.cs e Tests/PlayMode/CorridorDemonSceneTests.cs: contratos e testes na escola real.

## Ajustes

No asset Data/CorridorDemon20261105/CorridorAtmosphere.asset: fogColor preto, fogDensity 0,34, visibility 12m (10–14), flashlightRange 9m, lightIntensity 0,5, lightRange 7m, lightFadeSeconds 0,45, litNeighbours 1. Alterar a cor e a densidade ali. A névoa é do ambiente, exponencial, simétrica; não é uma camada cinza na câmera. O material e a iluminação original são restaurados ao alcançar a sala correta.

moduleLength 13,5m corresponde à geometria extraída e ao wrap; não alterar isoladamente sem reconstruir os cinco módulos. Os buffers são apenas visuais: sem IDs, interações ou colisores duplicados; as portas centrais mantêm suas regras. Contagens crescem até 12 grupos por parede; depois a quantidade permanece limitada. As oito variantes de arranhões escurecem e ganham relevo nas primeiras voltas. Mãos pequenas se aproximam da porta correta. Os arranhões das portas centrais são filhos das folhas móveis; cópias visuais ficam apenas nos buffers. Atualizações visuais aguardam sair do campo de visão. Na primeira ativação ou restauração, o estágio salvo é montado antes de exibir os módulos.

No DemonVoice.asset: layerDelay 0,08–0,15s (média usada), maskGain 0,64, lowGain 0,44, voiceCeiling 0,8, fallbackPitch 0,56, slowPitch 0,9, choirGain 0,045, voiceDistance 9m e maxDistance 24m. Estágios por wrap: 0 → máscara; 1 → mistura; 2 ou mais → baixa. O contador antigo loopCount continua limitado a 3; wrapCount opcional mantém as voltas seguintes sem mudar IDs/versão. O estágio deriva desse contador no carregamento.

Mixer: grupos filhos do Voice. Baixa: Pitch Shifter, Distortion leve, Lowpass 1900Hz, SFX Reverb e Compressor. DemonLowPitch exposto em 0,56 quando falta take baixo; 1 quando existe take próprio. Estágio 3 aplica ainda pitch de reprodução 0,9. Ganhos das falas limitados em conjunto e grupos com -3dB de margem. Ajustar artisticamente com os takes finais e conferir picos depois dos efeitos; teto matemático de ganho não substitui audição nem medição da mixagem final.

Espelho: a oferta original permanece numa única fala, inicia na máscara e cede para a baixa, preservando os índices e as vozes de João/crianças. “O senhor não deveria ter feito isso.” usa só a baixa. Legenda única para as duas fontes.

## Gravações e arte pendentes

Os 32 slots de máscara e 32 de baixa ainda não possuem os takes novos. Nada foi associado a uma legenda diferente. A produção continua com legendas provisórias; o fallback só produz som quando houver ao menos a gravação da máscara. Arrastar os arquivos indicados na planilha aos campos mask/low da entrada correspondente no DemonVoice.asset. Os slots choirHum/choirHumReversed aguardam a mixagem da sessão dos sete dubladores; não há canto fabricado ou TTS.

Não faltam imagens para as marcas procedurais do corredor. A primeira foto do lote artístico do espelho foi gerada como prévia separada, sem substituir a sequência atual; próximas imagens dependem do “próxima” e das referências previstas no pedido artístico. A prévia nativa é 1086x1448, abaixo da resolução mínima solicitada; não foi apresentada como 1536x2048.

## Testes e limites

Resultados reais, capturas e relatório final ficam em outputs/CorridorDemon20261105. A dublagem final, o coro, o equilíbrio artístico em fones/caixas, a campanha inteira e um executável novo são NÃO VERIFICADO. Nenhum ZIP ou build novo foi criado. Não houve novos downloads nem consumo adicional do ElevenLabs.
