// Reúne o conteúdo e os padrões da campanha anterior.

using System;

namespace HorrorGame
{
    /// <summary>
    /// Narrative data; chapter headings follow the current psychological-horror brief.
    /// Empty scene names deliberately leave future maps unavailable.
    /// Recorded voices are assigned to the saved story assets after populating captions.
    /// </summary>
    public static class StoryContent
    {
        public static void Populate(StoryBook book)
        {
            if (!book) throw new ArgumentNullException(nameof(book));

            book.chapters = new[]
            {
                Chapter("prologue", "O Telefone", "CAPÍTULO 1", "Chapter1Office",
                    "Atender o telefone.", "",
                    Milestone("prologue.first-call", "Registros de Daniel: biblioteca da escola. Antes de sair, atender o telefone novamente."),
                    Milestone("prologue.second-call", "Pegar os equipamentos na gaveta da mesa."),
                    Milestone("prologue.equipment", "Sair do escritório.", "", "office_equipment"),
                    Milestone("prologue.exit", "Ir até a escola abandonada.")),

                Chapter("road", "A Chegada", "CAPÍTULO 2", "RoadJourney",
                    "Chegar à escola e entrar na biblioteca.", "A porta estava esperando por ele.",
                    Milestone("road.context-shown", "Chegar à região indicada pelo telefone."),
                    Milestone("road.car-parked", "Atender o telefone antes de sair do carro."),
                    Milestone("road.arrival-call", "Olhar para a escola."),
                    Milestone("road.window-lit", "Pegar a lanterna."),
                    Milestone("road.flashlight-taken", "Sair do carro pela porta do motorista."),
                    Milestone("road.car-exited", "Entrar pelo portão e seguir até a biblioteca."),
                    Milestone("road.school-gate", "Entrar na biblioteca pela porta entreaberta.")),

                Chapter("school", "A Escola Abandonada", "CAPÍTULO 3", "SchoolCEEP",
                    "Explorar a entrada e descobrir o que aconteceu no local.",
                    "A escola não está tão vazia quanto parece.",
                    Milestone("school.entered", "Verificar a porta principal."),
                    Milestone("school.entry-locked", "Encontrar os registros na Secretaria, pelo corredor central."),
                    Milestone("school.documents-found", "Investigar a fotografia antiga da turma."),
                    Milestone("school.photograph", "Investigar o quadro da Sala 02, no corredor de salas."),
                    Milestone("school.blackboard-seen", "Verificar o corredor."),
                    Milestone("school.blackboard-vanished", "Investigar a Sala 03, ao lado da Sala 02."),
                    Milestone("school.ghost-approached", "Investigar o desenho deixado no chão."),
                    Milestone("school.drawing", "Continuar a investigação.", "", "school_drawing"),
                    Milestone("school.blackout", "Explorar a área mais antiga da escola.")),

                Chapter("left-behind", "O Que Foi Deixado para Trás", "CAPÍTULO 4", "",
                    "Investigar os desaparecimentos e encontrar a chave antiga.", "",
                    Milestone("left-behind.documents-found", "Investigar a fotografia antiga."),
                    Milestone("left-behind.photograph", "Encontrar a chave antiga."),
                    Milestone("left-behind.key-found", "Voltar pelo corredor.", "", "old_key"),
                    Milestone("left-behind.demon-appeared", "Continuar pelo corredor."),
                    Milestone("left-behind.chase-started", "Seguir pelo corredor apesar da presença."),
                    Milestone("left-behind.safe-place", "Investigar a mensagem na parede."),
                    Milestone("left-behind.wall-message", "Usar a chave antiga na porta bloqueada."),
                    Milestone("left-behind.locked-area-opened", "Explorar a área antiga da escola.")),

                Chapter("memories", "As Memórias", "CAPÍTULO 5", "",
                    "Descobrir a ligação de João com a escola.", "",
                    Milestone("memories.past-echoes", "Investigar o gravador antigo."),
                    Milestone("memories.recording-one-found", "Ouvir a primeira gravação.", "memories.recording-one"),
                    Milestone("memories.recording-one", "Encontrar a outra gravação."),
                    Milestone("memories.recording-two-found", "Ouvir a segunda gravação.", "memories.recording-two"),
                    Milestone("memories.recording-two", "Investigar a fotografia do grupo de crianças."),
                    Milestone("memories.childhood-recognized", "Seguir a indicação do fantasma."),
                    Milestone("memories.ghost-appeared", "Observar a escada indicada pelo fantasma.", "memories.ghost"),
                    Milestone("memories.staircase-indicated", "Chegar ao terraço.")),

                Chapter("terrace", "O Terraço", "CAPÍTULO 6", "",
                    "Chegar ao terraço da escola.", "",
                    Milestone("terrace.ascent-started", "Continuar subindo as escadas.", "terrace.voices"),
                    Milestone("terrace.voices-finished", "Chegar à porta do terraço."),
                    Milestone("terrace.entered", "Observar o terraço."),
                    Milestone("terrace.door-closed", "Descobrir a verdade sobre o retorno de João."),
                    Milestone("terrace.cycle-revelation", "Encontrar uma saída."),
                    Milestone("terrace.final-chase", "Atravessar o terraço e descer pela escola."),
                    Milestone("terrace.descent-exit", "Chegar à entrada da escola.")),

                Chapter("last-exit", "A Última Saída", "CAPÍTULO FINAL", "",
                    "Escapar da escola com vida.", "",
                    Milestone("last-exit.entrance-reached", "Atravessar a porta principal."),
                    Milestone("last-exit.outside", "Afastar-se da escola."),
                    Milestone("last-exit.phone-ringing", "Atender o telefone."),
                    Milestone("last-exit.final-call", "Olhar para a escola."),
                    Milestone("last-exit.last-window", "Observar a janela do último andar.", "ending"),
                    Milestone("last-exit.ending", ""))
            };

            book.sequences = new[]
            {
                Sequence("prologue.call-one", "prologue.first-call", true,
                    Line("Homem idoso", "Senhor Claus... meu filho Daniel sumiu naquela escola há trinta anos.", 6f),
                    Line("Homem idoso", "Hoje ele me ligou. Disse que ainda está lá dentro, no Bloco Hospício.", 6f),
                    Line("João", "Por que você ligou para mim?", 3f),
                    Line("Homem idoso", "Ele disse que só você sabe qual porta abrir.", 4f),
                    Line("João", "Anotei o endereço.", 2f),
                    Line("João", "Espere. Como Daniel conseguiu este número?", 4f),
                    Line("", "[A ligação cai.]", 2f),
                    Pause(1f)),

                Sequence("prologue.call-two", "prologue.second-call", true,
                    Pause(2f),
                    Line("Voz infantil", "João..."),
                    Pause(1f)),

                Sequence("road.context", "road.context-shown", false,
                    Line("Rádio", "A escola está abandonada há décadas.", 4f),
                    Line("Rádio", "O último registro dos alunos desaparecidos veio do chamado Bloco Hospício.", 5f),
                    Line("João", "Hospício... Só um apelido de bloco escolar. Preciso começar pela biblioteca.", 5f)),

                Sequence("road.arrival-call", "road.arrival-call", true,
                    Line("", "[Sons de crianças brincando.]", 4f),
                    Line("Voz infantil", "Você demorou."),
                    Pause(1f)),

                Sequence("school.photo-inscription", "school.photograph", true,
                    Line("Verso da fotografia", "Alguns nunca foram embora.")),

                Sequence("school.blackboard-inscription", "school.blackboard-seen", true,
                    Line("Quadro", "JOÃO, VOCÊ VOLTOU.")),

                Sequence("school.drawing-inscription", "school.drawing", true,
                    Line("Desenho", "NÃO SUBA.")),

                Sequence("left-behind.photo-inscription", "left-behind.photograph", true,
                    Line("Verso da fotografia", "João de Claus — aluno.")),

                Sequence("left-behind.wall-inscription", "left-behind.wall-message", true,
                    Line("Parede", "ELE LEMBROU DE VOCÊ.")),

                Sequence("memories.recording-one", "memories.recording-one", true,
                    Line("Voz infantil", "Se alguém encontrar isso, não suba.", 4f),
                    Pause(3f),
                    Line("Voz infantil", "Ele fica esperando lá em cima."),
                    Line("João", "Quem?"),
                    Pause(1f)),

                Sequence("memories.recording-two", "memories.recording-two", true,
                    Line("Voz infantil", "João prometeu que voltaria."),
                    Pause(1f)),

                Sequence("memories.ghost", "memories.staircase-indicated", true,
                    Line("Fantasma", "Você prometeu."),
                    Pause(1f)),

                Sequence("terrace.voices", "terrace.voices-finished", false,
                    Line("Voz", "João..."),
                    Pause(2f),
                    Line("Voz", "Não vai embora..."),
                    Pause(2f),
                    Line("Voz", "Você voltou..."),
                    Pause(2f),
                    Line("Voz", "Ele está esperando.")),

                Sequence("last-exit.final-call", "last-exit.final-call", true,
                    Pause(3f),
                    Line("Voz infantil", "Obrigado por voltar."),
                    Line("João", "Quem é você?"),
                    Line("Voz infantil", "Você sabe."),
                    Pause(1f)),

                Sequence("ending", "last-exit.ending", true,
                    Pause(1f),
                    // MEMORY.md preserves the current game name despite the original script's "the".
                    Line("", "WHISPERS OF PAST", 4f),
                    Line("", "FIM", 4f))
            };
        }

        private static StoryChapterDefinition Chapter(
            string id, string title, string heading, string sceneName,
            string initialObjective, string completionPhrase,
            params StoryMilestoneDefinition[] milestones)
        {
            return new StoryChapterDefinition
            {
                id = id,
                title = title,
                heading = heading,
                sceneName = sceneName,
                initialObjective = initialObjective,
                completionPhrase = completionPhrase,
                milestones = milestones
            };
        }

        private static StoryMilestoneDefinition Milestone(
            string id, string objectiveAfter, string automaticSequenceId = "",
            params string[] requiredItemIds)
        {
            return new StoryMilestoneDefinition
            {
                id = id,
                objectiveAfter = objectiveAfter,
                automaticSequenceId = automaticSequenceId,
                requiredItemIds = requiredItemIds ?? Array.Empty<string>()
            };
        }

        private static StorySequenceDefinition Sequence(
            string id, string completionSignal, bool lockPlayer,
            params StoryLine[] lines)
        {
            return new StorySequenceDefinition
            {
                id = id,
                lockPlayer = lockPlayer,
                completionSignal = completionSignal,
                lines = lines
            };
        }

        private static StoryLine Line(string speaker, string text, float seconds = 3f)
        {
            return new StoryLine { speaker = speaker, text = text, seconds = seconds };
        }

        private static StoryLine Pause(float seconds)
        {
            return Line("", "", seconds);
        }
    }
}
