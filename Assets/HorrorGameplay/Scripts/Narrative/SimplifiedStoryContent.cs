// Monta as definições e itens do roteiro simplificado.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>The six-chapter school story; existing interaction and save IDs remain readable.</summary>
    public static class SimplifiedStoryContent
    {
        public static void Populate(StoryBook book)
        {
            StoryContent.Populate(book);
            book.revision = 2;
            var sequences = book.sequences.Where(s => s.id.StartsWith("prologue.") || s.id.StartsWith("road.")).ToList();
            book.chapters = new[] {
                book.chapters[0], book.chapters[1],
                Chapter("school", "A Biblioteca", 3, "Encontrar a biblioteca, no fim da passarela à direita.",
                    "A chave esperava por uma mão conhecida.",
                    Mark("school.entered", "Investigar a biblioteca. Siga o barulho entre as estantes."),
                    Mark("library.book-fell", "Ler o recorte de jornal sobre a mesa."),
                    Mark("library.newspaper", "Examinar a foto da turma e seu verso.", "library_newspaper"),
                    Mark("library.photo", "Procurar a Chave de Ferro junto ao livro caído.", "library_class_photo"),
                    Mark("library.iron-key", "Usar a Chave de Ferro na saída da biblioteca.", "iron_key"),
                    Mark("library.passage-opened", "Atravessar o pátio e chegar ao bloco Hospício.")),
                Chapter("left-behind", "Bloco Hospício — 1º andar", 4, "Seguir as placas até o Hospício. Encontrar a chave da Sala 113.",
                    "A voz da fita conhecia o caminho.",
                    Mark("hospicio.arrived", "Procurar o gravador na sala de aula aberta."),
                    Mark("hospicio.recorder", "Examinar a fita infantil e clicar em OUVIR.", "hospicio_recorder"),
                    Mark("hospicio.child-tape", "Investigar o diário de classe na sala aberta."),
                    Mark("hospicio.register", "Observar o fim do corredor.", "hospicio_register"),
                    Mark("hospicio.figure", "Procurar a fita do diretor na secretaria."),
                    Mark("hospicio.director-tape", "Encontrar a Chave da Sala 113 na secretaria."),
                    Mark("hospicio.room-key", "Subir a escada central e abrir a Sala 113.", "room_113_key")),
                Chapter("memories", "Sala 113", 5, "Subir a escada central e abrir a Sala 113.",
                    "Daniel era o nome escrito antes de João.",
                    Mark("memories.upstairs", "Seguir o corredor do segundo andar."),
                    Mark("memories.corridor", "Entrar na Sala 113 e olhar o espelho."),
                    Mark("memories.mirror", "Examinar a ficha de aluno na Sala 113."),
                    Mark("memories.truth", "Encontrar uma saída.", "student_113_record")),
                Chapter("last-exit", "A Última Saída", 6, "Descer a escada e seguir a luz até a entrada.",
                    "O telefone ainda não terminou de tocar.",
                    Mark("last-exit.stairs", "Seguir a luz no corredor central."),
                    Mark("last-exit.loop", "Voltar à porta principal. A escola não pode escolher por você."),
                    Mark("last-exit.entrance-reached", "Atravessar a entrada da escola."),
                    Mark("last-exit.outside", ""), Mark("last-exit.awake", ""),
                    Mark("last-exit.phone-ringing", ""), Mark("last-exit.ending", ""))
            };
            sequences.AddRange(new[] {
                Sequence("library.book", "", false, Line("João", "Esse livro... não tinha ninguém ali.", 3), Line("Página marcada", "O ferro abre o caminho de quem voltou.", 3)),
                Sequence("hospicio.arrival", "", false, Line("João", "Hospício. Só um apelido para o bloco... por que ele sabia que eu viria?", 5)),
                Sequence("hospicio.figure-seen-caption", "", false, Line("João", "Ei... você está perdido?", 3)),
                Sequence("hospicio.figure-caption", "", false, Line("João", "Eu acabei de ver alguém ali.", 3)),
                Sequence("memories.corridor-caption", "", false, Line("Voz distante", "Daniel...", 3), Line("João", "Eu já passei por essa porta. Não é possível.", 4)),
                Sequence("memories.mirror-caption", "", true, Line("João", "Eu não sorri.", 3)),
                Sequence("memories.truth-caption", "", false, Line("João", "João de Claus. Daniel... essa letra é minha.", 4), Line("João", "Eu não lembro de ter saído daquela noite.", 4)),
                Sequence("last-exit.loop-caption", "", false, Line("Voz infantil", "Por aqui. Não olha para trás.", 4), Line("João", "A porta. Só preciso chegar à porta.", 3)),
                Sequence("simplified.office-awake", "last-exit.awake", true, Line("João", "Eu... voltei?", 4)),
                Sequence("simplified.office-ring", "last-exit.phone-ringing", true, Line("", "", 5)),
                Sequence("ending", "last-exit.ending", true, Line("", "", .15f))
            });
            book.sequences = sequences.ToArray();
        }

        public static bool Migrate(NarrativeProgress progress, Inventory inventory)
        {
            if (progress == null || progress.storyRevision >= 2) return false;
            string oldChapter = progress.chapterId;
            progress.storyRevision = 2;
            if (oldChapter == "prologue" || oldChapter == "road") return false;
            if (oldChapter == "terrace") progress.chapterId = "last-exit";
            progress.pendingChapterId = "";
            // Keep old evidence in the notebook; grant replacement keys only for chapters already reached.
            bool afterLibrary = progress.chapterId == "left-behind" || progress.chapterId == "memories" || progress.chapterId == "last-exit";
            bool afterFirstFloor = progress.chapterId == "memories" || progress.chapterId == "last-exit";
            if (progress.milestones == null) progress.milestones = new List<string>();
            AddSignal(progress, "school.entered", afterLibrary || progress.HasSignal("school.entered"));
            if (afterLibrary)
            {
                foreach (string id in new[] { "library.book-fell", "library.newspaper", "library.photo", "library.iron-key", "library.passage-opened" }) AddSignal(progress, id, true);
                AddItem(inventory, "iron_key", "Chave de Ferro");
            }
            if (afterFirstFloor)
            {
                foreach (string id in new[] { "hospicio.arrived", "hospicio.recorder", "hospicio.child-tape", "hospicio.register", "hospicio.figure", "hospicio.director-tape", "hospicio.room-key" }) AddSignal(progress, id, true);
                AddItem(inventory, "room_113_key", "Chave da Sala 113");
                AddItem(inventory, "hospicio_recorder", "Gravador antigo");
            }
            if (progress.chapterId == "last-exit")
                foreach (string id in new[] { "memories.upstairs", "memories.corridor", "memories.mirror", "memories.truth" }) AddSignal(progress, id, true);
            // A completed old ending remains completed; an in-progress old ending can leave the school.
            if (progress.endingSeen) foreach (string id in new[] { "last-exit.stairs", "last-exit.loop", "last-exit.entrance-reached", "last-exit.outside", "last-exit.awake", "last-exit.phone-ringing", "last-exit.ending" }) AddSignal(progress, id, true);
            return true;
        }
        static void AddSignal(NarrativeProgress p, string id, bool condition) { if (condition && !p.HasSignal(id)) p.milestones.Add(id); }
        static void AddItem(Inventory inventory, string id, string title) { if (inventory) inventory.Add(new InventoryItem { id = id, title = title, text = "Preservado do checkpoint anterior." }); }
        static StoryChapterDefinition Chapter(string id, string title, int number, string objective, string phrase, params StoryMilestoneDefinition[] marks)
            => new StoryChapterDefinition { id = id, title = title, heading = "CAPÍTULO " + number, sceneName = "SchoolCEEP", initialObjective = objective, completionPhrase = phrase, milestones = marks };
        static StoryMilestoneDefinition Mark(string id, string objective, params string[] items)
            => new StoryMilestoneDefinition { id = id, objectiveAfter = objective, requiredItemIds = items };
        static StorySequenceDefinition Sequence(string id, string completion, bool locked, params StoryLine[] lines)
            => new StorySequenceDefinition { id = id, completionSignal = completion, lockPlayer = locked, lines = lines };
        static StoryLine Line(string who, string text, float seconds) => new StoryLine { speaker = who, text = text, seconds = seconds };
    }
}
