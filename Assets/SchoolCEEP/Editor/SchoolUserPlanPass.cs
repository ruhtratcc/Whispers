// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Explicit annotations confirmed against the user's colored plan and native CAD.</summary>
    public static class SchoolUserPlanPass
    {
        [Serializable] public sealed class RemovedSpan
        {
            public int level;
            public string block, reason;
            public bool vertical;
            public float p,a,b;
        }
        [Serializable] public sealed class Report
        {
            public string coloredPlan="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/SchoolCEEP/User-colored-plan-20261003.png";
            public string legend="Blue=walls; yellow=doors. Hand-drawn topology uses local building landmarks, not a global metric scale.";
            public RemovedSpan[] removed;
        }
        static readonly RemovedSpan[] Confirmed = {
            new RemovedSpan{level=0,block="main_laboratories_north",vertical=false,p=794.36f,a=553.12f,b=829.48f,reason="Limite da PASSARELA COBERTA: linha simples .72 com tracejado paralelo y795.56 no P01, cotas e PLACA abaixo. Não é alvenaria; preservar a lateral estrutural real x829.48."},
            new RemovedSpan{level=0,block="west_south",vertical=false,p=855.32f,a=368.92f,b=469.48f,reason="Cota horizontal 700 dentro da Sala30 ou do Laboratório de Instalações Elétricas; não é divisória."},
            new RemovedSpan{level=0,block="west_south",vertical=false,p=1023.32f,a=497.92f,b=597.04f,reason="Cota horizontal 700 dentro da Sala30 ou do Laboratório de Instalações Elétricas; não é divisória."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=278.36f,b=348.08f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=358.76f,b=402.267f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=415.733f,b=428.48f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=428.84f,b=498.92f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=509.6f,b=543.268f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.36f,a=556.732f,b=579.68f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.48f,a=580.4f,b=650.24f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.48f,a=660.8f,b=704.268f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=true,p=496.48f,a=717.732f,b=730.64f,reason="Segmento da cota vertical 1005, paralela à parede real x502.12; a linha foi recortada junto às portas existentes."},
            new RemovedSpan{level=0,block="west_north",vertical=false,p=291.32f,a=402.88f,b=502.84f,reason="Cota horizontal 700 desenhada dentro de uma das três salas, sob sua parede norte verdadeira."},
            new RemovedSpan{level=0,block="west_north",vertical=false,p=441.92f,a=402.88f,b=502.84f,reason="Cota horizontal 700 desenhada dentro de uma das três salas, sob sua parede norte verdadeira."},
            new RemovedSpan{level=0,block="west_north",vertical=false,p=591.2f,a=401.44f,b=502.12f,reason="Cota horizontal 700 desenhada dentro de uma das três salas, sob sua parede norte verdadeira."},
            new RemovedSpan{level=0,block="main_middle_circulation",vertical=false,p=837.98f,a=1153.36f,b=1176.76f,reason="Topo do retângulo PLACA pareado com a cota 2570; uma anotação de incêndio virou uma falsa parede."},
            new RemovedSpan{level=0,block="main_middle_circulation",vertical=false,p=839.72f,a=829.48f,b=1210.12f,reason="Cota 2570 atravessa a circulação aberta; não é alvenaria."},
            new RemovedSpan{level=0,block="proposed_library",vertical=true,p=1910.56f,a=776.6f,b=874.16f,reason="Vertical 1460 dimension inside library, with text gap and diagonal ticks in native PDF; colored plan shows a single reading room."},
            new RemovedSpan{level=0,block="proposed_library",vertical=true,p=1910.56f,a=884.72f,b=982.16f,reason="Vertical 1460 dimension inside library, with text gap and diagonal ticks in native PDF; colored plan shows a single reading room."},
            new RemovedSpan{level=0,block="proposed_east_teaching",vertical=false,p=774.92f,a=2400.28f,b=2407.36f,reason="False pairing of facade dashed line y773.12 and PLACA annotation y776.72; yellow doorway in colored plan. Preserve masonry jambs."},
        };
        public static Report Prepare(SchoolMapBuilder.Layout layout)
        {
            var removed=new List<RemovedSpan>();
            foreach(var level in layout.levels)
            {
                var keep=new List<SchoolMapBuilder.Span>();
                foreach(var span in level.walls)
                {
                    var match=Confirmed.FirstOrDefault(s=>s.level==level.index&&s.block==span.block&&s.vertical==span.vertical
                        &&Near(s.p,span.p)&&Near(s.a,span.a)&&Near(s.b,span.b));
                    if(match==null)keep.Add(span);else removed.Add(match);
                }
                level.walls=keep.ToArray();
            }
            if(removed.Count!=Confirmed.Length)throw new InvalidOperationException("Colored-plan annotation registry does not match source layout: "+removed.Count+" / "+Confirmed.Length);
            return new Report{removed=removed.ToArray()};
        }
        static bool Near(float a,float b)=>Mathf.Abs(a-b)<.03f;
    }
}
