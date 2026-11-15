using System.Collections.Generic;
using MissaoMarte.Events;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    /// <summary>
    /// Tutorial de uma máquina da base: o que ela faz, por que a NASA precisa dela em Marte e um caso real.
    /// Mostrado antes do primeiro uso. Texto para crianças de 7 a 12 anos.
    /// </summary>
    public sealed class MachineGuide
    {
        public MachineGuide(string id, string name, string question, string whatItDoes, string whyOnMars, string realFact, string source)
        {
            Id = id;
            Name = name;
            Question = question;
            WhatItDoes = whatItDoes;
            WhyOnMars = whyOnMars;
            RealFact = realFact;
            Source = source;
        }

        /// <summary>Equipamento (<see cref="EquipmentIds"/>), vedação ou uma das constantes de <see cref="MachineGuides"/>.</summary>
        public string Id { get; }

        public string Name { get; }

        /// <summary>Pergunta mostrada como ação antes do primeiro uso ("O que faz o gerador de O2?").</summary>
        public string Question { get; }

        public string WhatItDoes { get; }

        public string WhyOnMars { get; }

        /// <summary>Um caso real de uma missão espacial.</summary>
        public string RealFact { get; }

        /// <summary>De onde vem o caso real (curto, aparece na tela).</summary>
        public string Source { get; }
    }

    /// <summary>
    /// Os tutoriais das máquinas. Os casos reais foram conferidos em 14/11/2026:
    ///   MOXIE: NASA, "NASA's Oxygen-Generating Experiment MOXIE Completes Mars Mission" (06/09/2023), 122 g de O2.
    ///   Apollo 13: NASA History, adaptador improvisado para os filtros de CO2 (abril de 1970).
    ///   Água: NASA, "NASA Achieves Water Recovery Milestone on International Space Station" (20/06/2023), 98%.
    ///   Phoenix: NASA/JPL, "Bright Chunks at Phoenix Lander's Mars Site Must Have Been Ice" (19/06/2008).
    ///   Vazamento: Roscosmos, confirmado pela NASA (outubro de 2020), folhas de chá no módulo Zvezda.
    ///   Perseverance: NASA, coleta de amostras em tubos para o Mars Sample Return.
    ///   InSight: NASA, "NASA Retires InSight Mars Lander Mission After Years of Science" (21/12/2022).
    ///   Estação Espacial: uma volta na Terra a cada ~90 min, cerca de 16 nasceres do Sol por dia (NASA).
    /// </summary>
    public static class MachineGuides
    {
        /// <summary>Laboratório (não é um equipamento da simulação).</summary>
        public const string Lab = "lab";

        /// <summary>Painéis solares (não são um equipamento ligável da simulação).</summary>
        public const string Panels = "panels";

        static readonly MachineGuide[] all =
        {
            new MachineGuide(EquipmentIds.OxygenGenerator, "Gerador de O2", "O que faz o gerador de O2?",
                "Usa eletricidade para separar a água em duas partes: hidrogênio e oxigênio. O oxigênio vai para o ar da base.",
                "O ar de Marte é quase todo gás carbônico: ninguém consegue respirar lá fora. E levar todo o oxigênio da Terra pesaria demais no foguete.",
                "O MOXIE, uma máquina que foi a Marte no rover Perseverance, fez 122 gramas de oxigênio com o próprio ar de Marte.",
                "NASA, missão MOXIE (2021 a 2023)"),

            new MachineGuide(EquipmentIds.Co2Scrubber, "Suporte atmosférico", "O que faz o suporte atmosférico?",
                "Puxa o ar da base e tira dele o gás carbônico (CO2), o gás que a gente solta toda vez que respira.",
                "A base é fechada: o CO2 da respiração não tem para onde ir e vai se acumulando. Em excesso, dá dor de cabeça e fica perigoso.",
                "Na Apollo 13, o CO2 começou a subir dentro da nave. Os astronautas montaram um encaixe para os filtros com fita adesiva, plástico e papelão, e voltaram bem.",
                "NASA, missão Apollo 13 (1970)"),

            new MachineGuide(EquipmentIds.WaterRecycler, "Tratamento de água", "O que faz o tratamento de água?",
                "Limpa a água já usada (da pia, do banho e até do suor e do xixi) e devolve água limpa para beber.",
                "Em Marte não tem rio nem torneira. Cada litro trazido da Terra custa muito; reciclar faz a água da base durar muito mais.",
                "Na Estação Espacial, a máquina de reciclar água consegue recuperar 98 de cada 100 litros usados.",
                "NASA, Estação Espacial Internacional (2023)"),

            new MachineGuide(EquipmentIds.IceProcessor, "Estação de gelo", "O que faz a estação de gelo?",
                "Aquece o gelo que você traz de fora, derrete e limpa a água para a base usar.",
                "Marte tem gelo escondido debaixo do chão. Usar o que já existe lá é muito mais fácil do que trazer tudo da Terra.",
                "Em 2008, a sonda Phoenix cavou o chão de Marte e achou pedacinhos brancos. Em 4 dias eles sumiram no ar: eram gelo de água!",
                "NASA, sonda Phoenix (2008)"),

            new MachineGuide(EquipmentIds.Infrastructure, "Iluminação", "O que faz a iluminação?",
                "Acende as luzes da base. Com luz, você enxerga o que faz e conserta as máquinas na metade do tempo.",
                "A noite em Marte é longa e escura, e na tempestade de poeira até o dia escurece. Trabalhar no escuro é mais lento e arriscado.",
                "Na Estação Espacial o Sol nasce e se põe cerca de 16 vezes por dia. Lá, são as luzes da estação que marcam a hora de trabalhar e de dormir.",
                "NASA, Estação Espacial Internacional"),

            new MachineGuide(EquipmentFailure.SealTarget, "Vedação da escotilha", "O que faz a vedação?",
                "Fecha a escotilha bem apertada, para o ar de dentro da base não escapar.",
                "Lá fora, o ar de Marte é mais de 100 vezes mais fraco que o da Terra. Por qualquer fresta, o ar da base vaza para fora.",
                "Em 2020, os astronautas da Estação Espacial acharam um vazamento soltando folhas de chá no ar: elas flutuaram direto para a rachadura.",
                "Roscosmos e NASA (2020)"),

            new MachineGuide(Lab, "Laboratório", "O que faz o laboratório?",
                "Analisa as rochas que você traz de fora para descobrir do que Marte é feito.",
                "As rochas contam a história do planeta: se Marte já teve rios e lagos e se um dia pode ter tido vida.",
                "O rover Perseverance guarda pedacinhos de rocha de Marte em tubos, para um dia serem trazidos e estudados na Terra.",
                "NASA, rover Perseverance"),

            new MachineGuide(Panels, "Painéis solares", "O que fazem os painéis solares?",
                "Transformam a luz do Sol em eletricidade e carregam a bateria da base.",
                "Tudo na base precisa de energia: o ar, a água e a luz. Em Marte, a poeira cobre os painéis e eles geram menos: é preciso limpar.",
                "A sonda InSight trabalhou mais de 4 anos em Marte com painéis solares. Em 2022 a poeira cobriu os painéis, a energia acabou e ela se desligou.",
                "NASA, sonda InSight (2018 a 2022)"),
        };

        static readonly Dictionary<string, MachineGuide> byId = BuildIndex();

        public static IReadOnlyList<MachineGuide> All => all;

        /// <summary>O tutorial da máquina, ou null se não houver um para esse id.</summary>
        public static MachineGuide For(string id)
        {
            return id != null && byId.TryGetValue(id, out MachineGuide guide) ? guide : null;
        }

        static Dictionary<string, MachineGuide> BuildIndex()
        {
            var index = new Dictionary<string, MachineGuide>();
            foreach (MachineGuide guide in all)
                index[guide.Id] = guide;
            return index;
        }
    }
}
