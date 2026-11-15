using System.Globalization;
using MissaoMarte.Events;
using MissaoMarte.Simulation;

namespace MissaoMarte.Game
{
    /// <summary>
    /// Textos curtos em português. O motor só produz dados; a tradução para texto fica aqui,
    /// fora da simulação e fora do código de tela.
    /// </summary>
    public static class Explanations
    {
        public const string IceNote =
            "Gelo de Marte não se bebe direto: leve-o à estação de gelo para extrair e purificar a água.";

        /// <summary>Formata um número com vírgula decimal, sem depender da cultura do sistema.</summary>
        public static string Number(double value, int decimals = 1)
        {
            string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            return value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
        }

        public static string EquipmentName(string equipmentId)
        {
            switch (equipmentId)
            {
                case EquipmentIds.OxygenGenerator: return "Gerador de O2";
                case EquipmentIds.Co2Scrubber: return "Suporte atmosférico";
                case EquipmentIds.WaterRecycler: return "Tratamento de água";
                case EquipmentIds.IceProcessor: return "Estação de gelo";
                case EquipmentIds.Infrastructure: return "Iluminação";
                default: return equipmentId;
            }
        }

        public static string RepairTargetName(string target)
        {
            return target == EquipmentFailure.SealTarget ? "Vedação" : EquipmentName(target);
        }

        /// <summary>Fato real mostrado ao analisar uma amostra.</summary>
        public static string ScienceFact(int sampleNumber)
        {
            switch ((sampleNumber - 1) % 3)
            {
                case 0: return "A gravidade de Marte é 38% da gravidade da Terra.";
                case 1: return "O ar de Marte é quase todo CO2 e tem menos de 1% da pressão da Terra.";
                default: return "Um sol, o dia marciano, dura 24 h 39 min.";
            }
        }

        public static string DescribeDefeat(DefeatReason reason)
        {
            switch (reason)
            {
                case DefeatReason.OxygenDepleted:
                    return "O oxigênio da base acabou. O gerador precisa de energia e de água, e vazamentos levam o ar embora.";
                case DefeatReason.Co2Toxic:
                    return "O CO2 ficou tóxico. Sem o suporte atmosférico, o gás que a tripulação expira se acumula.";
                case DefeatReason.WaterDepleted:
                    return "A água potável acabou. O tratamento devolve a água usada, e o gelo é a fonte nova.";
                case DefeatReason.EnergyFailure:
                    return "Falha de energia: a bateria zerou e o suporte à vida parou. Na tempestade, desligue o que não é essencial.";
                case DefeatReason.SuitOxygenDepleted:
                    return "O oxigênio do traje acabou lá fora. Volte para a base antes de o tanque esvaziar.";
                case DefeatReason.ObjectivesNotMet:
                    return "A tripulação sobreviveu, mas a base ainda não tem sua fonte de água: faltou tirar água do gelo.";
                default:
                    return string.Empty;
            }
        }

        public static string DescribeVictory(HabitatMission mission)
        {
            return "A base sobreviveu " + mission.Config.MissionSols + " sóis e ganhou sua primeira fonte: "
                + Number(mission.IceWaterProducedKg, 0) + " L de água do gelo de Marte.";
        }
    }
}
