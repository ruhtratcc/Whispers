// Reúne as definições de conteúdo da campanha atual.

using System;

namespace HorrorGame
{
    public static class BeforeDemolitionContent
    {
        public const string RadioAnnouncement = "...e para quem está chegando agora, hoje faz vinte anos que quatro alunos do SEEPE desapareceram. Marta, Caio, Luan e Bia. O caso foi encerrado como afastamento voluntário.";
        public static readonly string[] NormalNews = {
            "Boa madrugada. Você acompanha o boletim da madrugada. O movimento segue tranquilo nas ruas e nas rodovias da região.",
            "Equipes de manutenção trabalham em trechos sinalizados. Ao encontrar uma faixa interditada, reduza a velocidade e mantenha distância do veículo à frente.",
            "A previsão é de céu encoberto nas primeiras horas da manhã. Em áreas mais baixas, a visibilidade pode diminuir antes do nascer do sol.",
            "As primeiras linhas de ônibus começam a circular de acordo com os horários habituais. Os passageiros devem conferir o itinerário antes de sair de casa.",
            "O atendimento dos serviços municipais segue a programação regular. Solicitações feitas durante a madrugada serão encaminhadas às equipes de plantão.",
            "Para quem ainda está na estrada, atenção aos acostamentos. Se precisar parar, procure um local iluminado e sinalize o veículo.",
            "O dia começa daqui a pouco. Até lá, seguimos com as informações da madrugada. Dirija com cuidado e boa viagem."
        };
        public static void Populate(StoryBook book)
        {
            book.storyId = BeforeDemolitionCampaign.StoryId;
            book.revision = BeforeDemolitionCampaign.Revision;
            book.chapters = new[] {
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.OfficeChapterId, title = "O Escritório", heading = "CAPÍTULO 1",
                    sceneName = "Chapter1Office", initialObjective = "Ler os emails no computador.",
                    completionPhrase = "O caso não terminou em 2006.",
                    // Only the office flow can accept these; inventory alone must never trigger the exit.
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = BeforeDemolitionCampaign.ReadySignal, objectiveAfter = "Sair do escritório pela porta de entrada." },
                        new StoryMilestoneDefinition { id = BeforeDemolitionCampaign.ExitSignal }
                    }
                },
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.RoadChapterId, title = "A Estrada", heading = "CAPÍTULO 2", sceneName = "RoadJourney",
                    initialObjective = "Ir até a escola. O rádio continua ligado.", completionPhrase = "A secretaria ainda estava iluminada.",
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = "bd.road.radio-changed", objectiveAfter = "Confira a notificação no celular." },
                        new StoryMilestoneDefinition { id = "bd.road.car-parked", objectiveAfter = "Pegue a lanterna no banco do passageiro." },
                        new StoryMilestoneDefinition { id = "bd.road.flashlight-taken", objectiveAfter = "Saia pela porta do motorista." },
                        new StoryMilestoneDefinition { id = "bd.road.car-exited", objectiveAfter = "Siga até o portão da escola." },
                        new StoryMilestoneDefinition { id = "bd.road.school-gate" }
                    }
                },
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.SchoolChapterId, title = "Secretaria e Biblioteca", heading = "CAPÍTULO 3", sceneName = "SchoolCEEP",
                    initialObjective = "Encontrar a secretaria. A porta está aberta.", completionPhrase = "Ela nunca deixou de procurar.",
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = "bd.school.secretary", objectiveAfter = "Examine as pegadas no chão da secretaria." },
                        new StoryMilestoneDefinition { id = "bd.school.cups", objectiveAfter = "Procure a lancheira na gaveta da secretaria." },
                        new StoryMilestoneDefinition { id = "bd.school.lunchbox", objectiveAfter = "Pegue a chave dentro da lancheira." },
                        new StoryMilestoneDefinition { id = "bd.school.key", objectiveAfter = "Use a chave para entrar na biblioteca." },
                        new StoryMilestoneDefinition { id = "bd.school.library", objectiveAfter = "Examinar o mural da investigação e pegar o relatório no centro." },
                        new StoryMilestoneDefinition { id = "bd.school.report", objectiveAfter = "O telefone está tocando. Ouça a fita 1 até o fim para sair." },
                        new StoryMilestoneDefinition { id = "bd.school.tape", objectiveAfter = "Sair da biblioteca pela porta." },
                        new StoryMilestoneDefinition { id = "bd.school.exit" }
                    }
                },
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.FourChapterId, title = "Os Quatro", heading = "CAPÍTULO 4", sceneName = "SchoolCEEP",
                    initialObjective = "Investigar os quatro: Marta na sala de artes (03B), Caio no sanitário masculino, Luan no refeitório e Bia na Informática A.", completionPhrase = "Quatro lugares. A mesma noite.",
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = "bd.four.gathered", objectiveAfter = "Ir até a parte principal da escola." },
                        new StoryMilestoneDefinition { id = "bd.four.key", objectiveAfter = "Abrir a porta da escada e subir ao andar de cima." },
                        new StoryMilestoneDefinition { id = "bd.four.upstairs" }
                    }
                },
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.TwoVoicesChapterId, title = "Duas Vozes", heading = "CAPÍTULO 5", sceneName = "SchoolCEEP",
                    initialObjective = "Confira o celular e siga os sons baixos nas portas laterais.", completionPhrase = "A voz dela vinha do lugar errado.",
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = "bd.loop.escaped", objectiveAfter = "Ouvir a fita 3 da verdadeira Helena na sala lateral." },
                        new StoryMilestoneDefinition { id = "bd.loop.tape", objectiveAfter = "Ir para a sala do espelho." },
                        new StoryMilestoneDefinition { id = "bd.loop.mirror" }
                    }
                },
                new StoryChapterDefinition {
                    id = BeforeDemolitionCampaign.MirrorChapterId, title = "Espelho e Relatório", heading = "CAPÍTULO 6", sceneName = "SchoolCEEP",
                    initialObjective = "Examinar o espelho.", completionPhrase = "O que ficou no lugar ainda sabe seu nome.",
                    milestones = new[] {
                        new StoryMilestoneDefinition { id = "bd.mirror.seen", objectiveAfter = "Continue olhando para o espelho." },
                        new StoryMilestoneDefinition { id = "bd.mirror.broken", objectiveAfter = "Ouça o que Helena tem a dizer." },
                        new StoryMilestoneDefinition { id = "bd.mirror.choice", objectiveAfter = "Confira o que ficou depois daquela noite." },
                        new StoryMilestoneDefinition { id = "bd.mirror.finish" }
                    }
                }
            };
            book.sequences = new[] {
                Sequence("bd.four.intercom", true,
                    Line("Interfone — Helena", "Muito bem, João. Agora o senhor já sabe. Suba, por favor. É rápido. Eu preciso falar com o senhor.", 11),
                    Line("João", "Ela sabe o que eu encontrei. Mas quem está falando comigo?", 5)),
                Sequence("bd.loop.voice", false, Line("Helena, à frente", "Venha, João. Está quase.", 4)),
                Sequence("bd.loop.phone", false, Line("Celular — Helena", "O senhor virou à esquerda. Eu preferiria que não.", 6)),
                Sequence("bd.mirror.offer", true,
                    Line("Helena", "Eles estão presos aqui por sua causa, João. Alguém precisa ficar no lugar deles pra eles saírem. O senhor sempre quis consertar isso.", 10),
                    Line("João", "Se eu ficar... eles saem.", 4),
                    Line("Crianças", "não", 2)),
                Sequence("bd.ending.refuse", true,
                    Line("João", "Meu Deus...", 3),
                    Line("Helena", "O senhor não deveria ter feito isso.", 5)),
                Sequence("bd.ending.sign", true,
                    Line("Helena", "Obrigada, João.", 4))
            };
        }
        static StoryLine Line(string speaker, string text, float seconds) => new StoryLine { speaker = speaker, text = text, seconds = seconds };
        static StorySequenceDefinition Sequence(string id, bool locked, params StoryLine[] lines) => new StorySequenceDefinition { id = id, lockPlayer = locked, lines = lines };
    }
}
