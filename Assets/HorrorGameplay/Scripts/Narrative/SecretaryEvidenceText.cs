// Sincroniza os textos das evidências entre versões de save.

using System.Collections.Generic;
namespace HorrorGame
{
    public static class SecretaryEvidenceText
    {
        public static string Refresh(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("Sr. João, já faz mais de um ano. O diretor Aurélio mandou eu parar de ligar. Guardei a lancheira da Marta na minha gaveta porque ninguém veio buscar. Semana passada eu abri. A merenda estava quente. Não é de hoje, é a merenda de 2006. Quente como se tivesse acabado de ser embalada. Joguei fora. Voltou pra gaveta no dia seguinte.", "Sr. João, já faz mais de um ano. O diretor Aurélio mandou eu parar de ligar. Guardei a lancheira da Marta na minha gaveta porque ninguém veio buscar. Semana passada abri e tinha um desenho novo lá dentro, de giz de cera, num papel que não é da escola. Uma casa, quatro bonequinhos e um homem alto na porta. Eu não coloquei nada ali. Joguei fora. Voltou pra gaveta no dia seguinte.");
            text = text.Replace("Sr. João, não consigo dormir. A energia da escola está cortada desde 2008. Mas ontem o sinal tocou. Eu moro a duas quadras. Fui até lá. A secretaria estava com a luz acesa e tinha quatro copos de suco na minha mesa, ainda gelados. Eu não deixei nada ali. Um dos copos tinha marca de dente na borda. Dente pequeno.", "Sr. João, não consigo dormir. A energia da escola está cortada desde 2008, mas ontem o sinal tocou e eu moro a duas quadras. Fui até lá. O relógio da secretaria estava parado em três e sete. No chão tinha pegadas pequenas na poeira, quatro pares, da porta até a escada. Eu varro e na manhã seguinte elas voltam. Todas vão pro andar de cima e param no fim do corredor.");
            text = text.Replace("mochila, casaco e merenda", "mochila, casaco e caderno");
            text = text.Replace("mochilas, casacos e merenda", "mochilas, casacos e cadernos");
            text = text.Replace("Os quatro copos continuam na mesa. Troquei o suco ontem. Hoje ele está outra vez como naquela noite. A merenda da Marta ainda está quente.", "Os desenhos continuam na gaveta. Joguei um fora. No dia seguinte ele estava lá de novo.");
            text = text.Replace("Quatro copos de suco. Ainda estão gelados. Um tem pequenas marcas de dentes na borda. Ninguém devia ter servido isso hoje.", "Quatro pares de pegadas pequenas na poeira. Saem da secretaria e seguem até a escada.");
            text = text.Replace("MARTA — 2006. A merenda continua quente. Há uma chave de latão dentro.", "MARTA — 2006. A lancheira guarda uma chave de latão e desenhos de giz de cera. O mais recente está marcado com a data de hoje: 26/10/2026.");
            text = text.Replace("Examine os quatro copos na mesa de Helena.", "Examine as pegadas no chão da secretaria.");
            text = text.Replace("Quatro copos gelados", "Pegadas pequenas na poeira");
            text = text.Replace("Examinar os quatro copos gelados", "Examinar as pegadas pequenas");
            text = text.Replace("Os copos... tão vibrando.", "As pegadas... estão aparecendo.");
            text = text.Replace("O som tá mexendo com o vidro.", "O som está mostrando as pegadas.");
            text = text.Replace("Segurando o sopro... olha os copos.", "As pegadas seguem até a escada.");
            text = text.Replace("Quatro copos. Sempre os quatro.", "Quatro pares de pegadas. Sempre os quatro.");
            text = text.Replace("Os copos. Preciso olhar de perto.", "As pegadas. Preciso olhar de perto.");
            text = text.Replace("Foram os copos que ela mencionou.", "Foram essas pegadas que ela mencionou.");
            text = text.Replace("Vou examinar os quatro copos na mesa.", "Vou examinar as pegadas no chão.");
            text = text.Replace("Preciso conferir o suco que ficou na mesa de Helena.", "Preciso conferir as pegadas que Helena mencionou.");
            text = text.Replace("Os copos ainda estavam gelados.", "As pegadas seguiam para a escada.");
            text = text.Replace("Aquele suco... ninguém devia ter servido.", "Aquelas pegadas... não eram minhas.");
            text = text.Replace("Quatro copos. Ainda gelados.", "Quatro pares. Pegadas pequenas.");
            text = text.Replace("Eu senti o frio. Não imaginei aquilo.", "Eu vi as marcas no chão. Não imaginei aquilo.");
            text = text.Replace("A merenda estava quente.", "Tinha um desenho de hoje na lancheira.");
            text = text.Replace("Aquela comida devia estar estragada.", "Como aquele desenho foi parar ali?");
            text = text.Replace("Não faz sentido continuar quente.", "A data naquele desenho não faz sentido.");
            text = text.Replace("Sr. João, já faz mais de um ano. O diretor Aurélio mandou eu parar de ligar. Guardei a lancheira da Marta na minha gaveta porque ninguém veio buscar. Semana passada abri e a merenda estava quente. Não é de hoje, é a merenda de 2006. Estava quente como se tivesse acabado de ser embalada. Joguei fora. Voltou pra gaveta no dia seguinte.", "Sr. João, já faz mais de um ano. O diretor Aurélio mandou eu parar de ligar. Guardei a lancheira da Marta na minha gaveta porque ninguém veio buscar. Semana passada abri e tinha um desenho novo lá dentro, de giz de cera, num papel que não é da escola. Uma casa, quatro bonequinhos e um homem alto na porta. Eu não coloquei nada ali. Joguei fora. Voltou pra gaveta no dia seguinte.");
            text = text.Replace("Sr. João, não consigo dormir. A energia da escola está cortada desde 2008, mas ontem o sinal tocou e eu moro a duas quadras. Fui até lá. A secretaria estava com a luz acesa e tinha quatro copos de suco na minha mesa, ainda gelados. Eu não deixei nada ali. Um dos copos tinha marca de dente na borda. Dente pequeno.", "Sr. João, não consigo dormir. A energia da escola está cortada desde 2008, mas ontem o sinal tocou e eu moro a duas quadras. Fui até lá. O relógio da secretaria estava parado em três e sete. No chão tinha pegadas pequenas na poeira, quatro pares, da porta até a escada. Eu varro e na manhã seguinte elas voltam. Todas vão pro andar de cima e param no fim do corredor.");
            return text;
        }
        public static void Refresh(List<InventoryItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null || !(item.id == "bd.cups" || item.id == "bd.lunchbox" || item.id == "bd.tape.01"
                    || item.id == "bd.note.email.01" || item.id == "bd.note.email.02" || item.id == "bd.note.email.03")) continue;
                item.title = Refresh(item.title); item.text = Refresh(item.text);
            }
        }
    }
}
