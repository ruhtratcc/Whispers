// Contém o texto, os anexos e as regras iniciais de entrega dos emails.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    [Serializable]
    public sealed class EmailParagraph
    {
        [TextArea(1, 6)] public string text;
        public bool child;
    }

    [Serializable]
    public sealed class EmailAttachment
    {
        public string fileName;
        public Texture2D preview;
        public bool unavailable;
    }

    [Serializable]
    public sealed class EmailMessage
    {
        public string id, sender, date, time, subject;
        public bool initiallyDelivered;
        public EmailParagraph[] paragraphs = Array.Empty<EmailParagraph>();
        public EmailAttachment[] attachments = Array.Empty<EmailAttachment>();
        public string PlainText => string.Join("\n", (paragraphs ?? Array.Empty<EmailParagraph>())
            .Select(paragraph => paragraph?.text ?? string.Empty));
    }

    [CreateAssetMenu(menuName = "Whispers/Email Catalog")]
    public sealed class EmailCatalog : ScriptableObject
    {
        public EmailMessage[] messages = Array.Empty<EmailMessage>();

        public EmailMessage Find(string id) => string.IsNullOrWhiteSpace(id) ? null :
            messages?.FirstOrDefault(message => message != null && message.id == id);

        public string[] ValidateContent()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (messages == null || messages.Length == 0) errors.Add("Catálogo de emails vazio.");
            foreach (var message in messages ?? Array.Empty<EmailMessage>())
            {
                if (message == null)
                {
                    errors.Add("Email nulo no catálogo.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(message.id) || !ids.Add(message.id))
                    errors.Add("Email sem ID único: " + message.id);
                if (string.IsNullOrWhiteSpace(message.sender)) errors.Add("Email sem remetente: " + message.id);
                if (!DateTime.TryParseExact(message.date, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                    errors.Add("Data de email inválida: " + message.id);
                if (!string.IsNullOrEmpty(message.time) && !DateTime.TryParseExact(message.time, "HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    errors.Add("Hora de email inválida: " + message.id);
                if (message.paragraphs == null || message.paragraphs.Length == 0 ||
                    message.paragraphs.Any(paragraph => paragraph == null || string.IsNullOrWhiteSpace(paragraph.text)))
                    errors.Add("Corpo de email vazio ou inválido: " + message.id);
                foreach (var attachment in message.attachments ?? Array.Empty<EmailAttachment>())
                    if (attachment == null || string.IsNullOrWhiteSpace(attachment.fileName))
                        errors.Add("Anexo sem nome de arquivo: " + message.id);
            }
            return errors.ToArray();
        }

        public static EmailMessage[] BeforeDemolitionContent() => new[]
        {
            new EmailMessage
            {
                id = "email.01", sender = "Helena Souza", date = "14/03/2006", time = "",
                subject = "Alunos da 7B, podemos conversar?", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "Sr. João, boa tarde. Meu nome é Helena Souza, sou secretária do SEEPE. O senhor esteve aqui na quinta. Queria avisar que os quatro deixaram mochila, casaco e caderno na sala. A Marta nunca sai sem o caderno de desenho. Não sei se isso ajuda. Ramal da secretaria: 204." }
                }
            },
            new EmailMessage
            {
                id = "email.02", sender = "Helena Souza", date = "09/04/2007", time = "",
                subject = "RE: Alunos da 7B", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "Sr. João, já faz mais de um ano. O diretor Aurélio mandou eu parar de ligar. Guardei a lancheira da Marta na minha gaveta porque ninguém veio buscar. Semana passada abri e tinha um desenho novo lá dentro, de giz de cera, num papel que não é da escola. Uma casa, quatro bonequinhos e um homem alto na porta. Eu não coloquei nada ali. Joguei fora. Voltou pra gaveta no dia seguinte." }
                }
            },
            new EmailMessage
            {
                id = "email.03", sender = "Helena Souza", date = "22/11/2010", time = "03:07",
                subject = "desculpa o horário", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "Sr. João, não consigo dormir. A energia da escola está cortada desde 2008, mas ontem o sinal tocou e eu moro a duas quadras. Fui até lá. O relógio da secretaria estava parado em três e sete. No chão tinha pegadas pequenas na poeira, quatro pares, da porta até a escada. Eu varro e na manhã seguinte elas voltam. Todas vão pro andar de cima e param no fim do corredor." }
                }
            },
            new EmailMessage
            {
                id = "email.04", sender = "Helena Souza", date = "03/06/2014", time = "03:07",
                subject = "o senhor leu", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "O aviso de leitura diz que o senhor abriu o email anterior às 03:07 de hoje. Estava acordado, então. Sentado na cadeira que range pra esquerda." },
                    new EmailParagraph { text = "Eu sei porque a Bia me contou. Ela disse que o senhor lê com a mão esquerda no mouse e que o polegar da direita fica na boca quando o senhor fica nervoso. Isso é verdade?" },
                    new EmailParagraph { text = "Eu moro na escola agora. Não vou mais pra casa." }
                }
            },
            new EmailMessage
            {
                id = "email.05", sender = "Helena Souza", date = "17/09/2016", time = "03:07",
                subject = "eu to bem", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "eu to bem to ótima João. Arranquei a unha do polegar ontem pra ver se doía. Não doeu. Não saiu sangue. Dentro tem giz." },
                    new EmailParagraph { text = "o senhor lembra do apito" },
                    new EmailParagraph { text = "não responde ela", child = true },
                    new EmailParagraph { text = "tem muita gente aqui dentro da minha cabeça e uma delas escreve melhor que eu" },
                    new EmailParagraph { text = "a gente tá atrás do espelho", child = true },
                    new EmailParagraph { text = "ela não dorme mais mas a gente dorme tudo junto", child = true },
                    new EmailParagraph { text = "Atenciosamente Helena (Helena) (Helena)" }
                }
            },
            new EmailMessage
            {
                id = "email.06", sender = "Helena Souza", date = "08/05/2018", time = "03:07",
                subject = "Peço desculpas pelos emails anteriores", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "Prezado Sr. João," },
                    new EmailParagraph { text = "Peço sinceras desculpas pelos emails anteriores. Eu estava doente, mas estou muito melhor. Ninguém o culpa." },
                    new EmailParagraph { text = "O senhor ainda guarda o apito do Luan na segunda gaveta, atrás da garrafa? Não precisa responder." },
                    new EmailParagraph { text = "Em resposta ao seu email de ontem:" },
                    new EmailParagraph { text = "> \"se eu abrir eu não fecho mais\"" },
                    new EmailParagraph { text = "Compreendo perfeitamente. Estou com o senhor." },
                    new EmailParagraph { text = "Atenciosamente, Helena Souza" },
                    new EmailParagraph { text = "ela morreu hoje de manhã", child = true },
                    new EmailParagraph { text = "quem tá escrevendo é o que ficou no lugar", child = true }
                }
            },
            new EmailMessage
            {
                id = "email.07", sender = "Helena Souza", date = "02/10/2022", time = "03:07",
                subject = "Demolição programada", initiallyDelivered = true,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "Prezado Sr. João," },
                    new EmailParagraph { text = "A demolição do SEEPE está autorizada para 27 de outubro, às 8h. Gostaria muito que o senhor viesse antes. Preciso falar com o senhor pessoalmente." },
                    new EmailParagraph { text = "Guardei o relatório de 2006. Venha antes da demolição. Há coisas que só o senhor pode esclarecer." },
                    new EmailParagraph { text = "É rápido. Eles esperam o senhor. A Marta fez uma carteira nova com o seu nome. Ficou torta. Ela tem só 12 anos." },
                    new EmailParagraph { text = "Atenciosamente, Helena" }
                },
                attachments = new[]
                {
                    new EmailAttachment { fileName = "relatorio_2006.pdf", unavailable = true },
                    new EmailAttachment { fileName = "relatorio_2via.pdf", unavailable = true },
                    new EmailAttachment { fileName = "foto_escritorio.jpg", unavailable = false }
                }
            },
            new EmailMessage
            {
                id = "email.08", sender = "Helena Souza", date = "26/10/2026", time = "03:07",
                subject = "", initiallyDelivered = false,
                paragraphs = new[]
                {
                    new EmailParagraph { text = "A porta da secretaria está aberta." },
                    new EmailParagraph { text = "Eu deixei a luz acesa." },
                    new EmailParagraph { text = "não vem", child = true },
                    new EmailParagraph { text = "Eu ouvi o senhor ligando o carro." }
                }
            }
        };
    }
}
