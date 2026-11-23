# Eventos de terror — passe 03/11/2026

| Evento | Gatilho/local | Intensidade e intervalo | Desativar/ajustar |
|---|---|---|---|
| Roedores dentro das paredes | Capítulos da escola, jogador livre; pontos ponderados na cantina, biblioteca, artes e corredores | Volume 0,09; 35–75s; fonte 3D, rolloff logarítmico e oclusão a 1100Hz; não repete ponto/guincho seguido | SchoolAmbientConfig.rats vazio ou componente desabilitado |
| Ruído distante/porta | 9–40m do jogador, sem narração | Volume 0,12; 95–160s; reutiliza gravações locais | distantObjects vazio |
| Luz breve sobrenatural | Entrada da biblioteca, quatro objetos reunidos, fita 3 ouvida | 3s, 1,25Hz limitado a 2Hz; perfis Mild/BriefFailure/Dying/Hum; restaura estado anterior | Reduzir efeitos ou eventLights vazio; SchoolAmbientConfig.lightProfile |
| Livro que cai/cadeira muda | Sistemas existentes da biblioteca, preservados | Eventos narrativos limitados, sem agressão | Seus componentes/flags existentes |
| Armário entreaberto | Leitura completa do email 7 | Animação suave 1,3s; luz interior 0,025–0,07; rangido baixo | OfficeWardrobe |
| Orientação | Sem progresso 100s, níveis seguintes; distância da rota >28m por 45s | 24s de separação; não encobre diálogo/fita/legenda importante | ContextBarks; configurações no Inspector |
| Espelho | Somente depois de escolher “Não confio em você” | Único susto forte; recuo até 1,25m/1,15s com teste de colisão/apoio | Reduzir efeitos remove interferência forte e suaviza tremor |
| Amanhecer/polícia | Final bom, após devolver apito | Varredura suave 0,25Hz, teto de 0,5Hz; sirene gravada 3D | EndingPresentationConfig |

As 98 falas novas estão no catálogo WhistleDialogue e na planilha. Leituras, orientação, ratos, ruídos e armário usam gatilhos. As variações de rádio e de final constam para gravação; o rádio usa seus textos revisados e os finais usam StoryBook/EndingPresentationConfig. Não se afirma que cada variação será ouvida em uma única partida.

Áudio ainda pendente: arranhões/roer/passos de rato especializados (buscar “mice in walls”, “rats scurrying”, “rodent chewing” no Freesound); zumbido/estalo para lightNoise; vozes humanas novas. Slots silenciosos permanecem identificados; sem TTS. Nenhum rato visual foi acrescentado (era opcional).
