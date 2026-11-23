// Define o conteúdo padrão do computador fictício.

using System;
using UnityEngine;

namespace HorrorGame.Desktop
{
    // These editable defaults only restate information already present in Helena's emails.
    public static class DesktopContentDefaults
    {
        public static DesktopNote[] Notes() => new[]
        {
            Note(1,
                P("Conferir os emails de Helena antes de sair.\nDemolição do SEEPE: 27/10/2026, às 8h.\n\nO arquivo se chama nao_ler. Já estava aberto."),
                P("o senhor lembra do apito", true)),
            Note(2,
                P("03:07 de novo.\nFechei esta janela antes. Não lembro de ter escrito a última linha."),
                P("não responde ela", true),
                P("O apito é do Luan. Isso eu sei.")),
            Note(3,
                P("Marta. Caio. Luan. Bia.\nSão quatro nomes no relatório de 2006. Preciso ler o que está escrito. Sem completar o que falta."),
                P("a gente tá atrás do espelho", true)),
            Note(4,
                P("Helena pediu para eu ir antes da demolição. Tem uma linha de aceite em branco.\n\nNão assinar nada sem ler. Não responder só pra fazer esta janela parar de aparecer."),
                P("quem tá escrevendo é o que ficou no lugar", true)),
            Note(5,
                P("27/10/2026. Oito da manhã.\nA data continua a mesma. O relógio também.\n\nLevar o apito do Luan."),
                P("não vem", true))
        };

        public static DesktopPage[] Pages() => new[]
        {
            Page("seepe.local/inicio", "SEEPE — página da escola",
                P("SEEPE\nArquivo da escola"),
                P("Arquivo da escola\n\nEsta cópia reúne o registro dos quatro alunos e o comunicado de demolição."),
                P("Secretaria: Helena Souza. Ramal 204.\n\nOs documentos preservados estão nos links acima.")),
            Page("seepe.local/arquivo/quatro-alunos", "Quatro alunos desaparecem na SEEPE",
                P("Quatro alunos desaparecem na SEEPE"),
                P("Marta, Caio, Luan e Bia são os quatro alunos citados no relatório de 2006. Os pertences ficaram na escola: mochilas, casacos e cadernos."),
                P("Em uma mensagem a João, a secretária Helena Souza contou que Marta nunca saía sem o caderno de desenho.\n\nO relatório de 2006 reúne os registros do caso. Esta cópia não traz novidades sobre o paradeiro dos alunos.")),
            Page("seepe.local/demolicao", "SEEPE — comunicado de demolição",
                P("COMUNICADO DE DEMOLIÇÃO"),
                P("SEEPE\nData prevista: 27 de outubro de 2026.\nHorário: 8h."),
                P("A demolição está autorizada. Helena Souza enviou em anexo o relatório de 2006 e a segunda via com a linha do aceite em branco."),
                P("O comunicado permanece disponível no arquivo da escola."))
        };

        public static DesktopError[] Errors() => new[]
        {
            Error("case", "CASO_ENCERRADO", "Não foi possível abrir o arquivo.\nPelo nome, o caso está encerrado. O conteúdo não confirma isso."),
            Error("attachment", "Anexo indisponível", "O anexo aparece na mensagem, mas não foi possível ler os dados.\nO nome do arquivo foi mantido."),
            Error("receipt", "Aviso de leitura", "Não foi possível confirmar esta leitura.\nJá existe um registro às 03:07."),
            Error("memory", "Memória em uso", "Este trecho já está aberto.\nFeche a outra janela e tente de novo."),
            Error("clock", "Relógio do sistema", DesktopSystemText.ClockChangeFailed),
            Error("disk", "Caminho indisponível", DesktopSystemText.OpenFileFailed)
        };

        public static DesktopTrack[] Tracks() => new[]
        {
            new DesktopTrack
            {
                id = "helena2006", title = "Recado de Helena — 2006", unlockVisit = 1,
                placeholderSeconds = 24
            },
            new DesktopTrack
            {
                id = "cantiga", title = "Cantiga de roda desafinada", unlockVisit = 2,
                placeholderSeconds = 28
            }
        };

        public static void Populate(DesktopContent content)
        {
            if (!content) throw new ArgumentNullException(nameof(content));
            content.notes = Notes();
            content.pages = Pages();
            content.errors = Errors();
            content.tracks = Tracks();
        }

        static EmailParagraph P(string text, bool child = false) =>
            new EmailParagraph { text = text, child = child };

        static DesktopNote Note(int visit, params EmailParagraph[] paragraphs)
        {
            var note = ScriptableObject.CreateInstance<DesktopNote>();
            note.name = "nao_ler_visita_" + visit.ToString("00");
            note.visit = visit;
            note.paragraphs = paragraphs;
            return note;
        }

        static DesktopPage Page(string address, string title, params EmailParagraph[] paragraphs)
        {
            var page = ScriptableObject.CreateInstance<DesktopPage>();
            page.name = "pagina_" + address.Replace('/', '_').Replace('.', '_');
            page.address = address;
            page.title = title;
            page.paragraphs = paragraphs;
            return page;
        }

        static DesktopError Error(string id, string title, string message)
        {
            var error = ScriptableObject.CreateInstance<DesktopError>();
            error.name = "erro_" + id;
            error.id = id;
            error.title = title;
            error.message = message;
            return error;
        }
    }
}
