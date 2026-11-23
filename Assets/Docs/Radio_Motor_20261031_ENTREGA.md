# Rádio e motor — entrega finalizada em31/10/2026

- Toda seleção de música, automática ou pelo botão, agora produz clique real e chiado curto0,32s. A música baixa brevemente para o som da troca aparecer. Gatilho centralizado em ApplyPhase; chamada duplicada do botão removida. Reconstruir checkpoint não toca clique/chiado de novo. Pausa congela o efeito.
- Dez falas reescritas para João falar de forma mais casual e menos descritiva. São legendas e slots de áudio humano existentes; roteiro acompanha, sem TTS.
- Causa real do motor apagado: arquivo original RMS−35,15dB contra música−11,59dB, diferença23,55dB. Cópia derivada RMS−20,5dB/peak−9,59dB, ganho+14,64dB sem clipping. Original preservado SHA256. Loop com crossfade60ms. Ganho da fonte.34→.6, filtro1800→2200Hz. Músicas normais.37→.27, noticiário/música assombrada.37 preservados. SFX/master pessoais preservados.

54testes únicos PASS: estrada19 + save35. Novos testes comprovam troca automática/manual/pausa/restore sem replay; leem amostras REAIS do clip importado e medem RMS/headroom, verificam motor junto da música e roteamentoSFX. Cenas/finais/flags da história preservados, FOV49. Compile sem erros. Testes temporários antesAwake, save pessoal não acessado. Editor devolvido aSchoolCEEP/parado/clean/testSaveOverrideNULL. Sem ZIP/download novo.

Arquivos: BeforeDemolitionRoadRadio.cs (gatilhos/volume/falas); BeforeDemolitionRoadFlow.cs (remove segundo gatilho); RoadJourney.unity (clip/ganho/filtro); car_interior_audible.wav (derivação real); BeforeDemolitionRoadSceneTests.cs (regressões); créditos/LICENCAS (adaptação do áudio local).

NÃO VERIFICADO: mix por escuta humana/hardware e partida standalone. Falas novas continuam aguardando gravação humana; não afirmar dublagem concluída.

Guard RoadMixWorkspace.End já executado; não repetir. Backups e scripts work/RoadMix20261030.
