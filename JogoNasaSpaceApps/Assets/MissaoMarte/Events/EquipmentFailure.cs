using System;

namespace MissaoMarte.Events
{
    /// <summary>
    /// Imprevisto do roteiro: em um ciclo fixo, um equipamento (ou a vedação da base) passa a ter uma
    /// condição pior. Não muda a física: muda o estado de uma peça, e a física faz o resto.
    /// </summary>
    public sealed class EquipmentFailure
    {
        /// <summary>Id usado para a vedação da base, que não é um equipamento elétrico.</summary>
        public const string SealTarget = "seal";

        public EquipmentFailure(int cycle, string target, double newCondition, string title)
        {
            if (cycle < 1) throw new ArgumentOutOfRangeException(nameof(cycle));
            if (string.IsNullOrEmpty(target)) throw new ArgumentException("O alvo do imprevisto é obrigatório.", nameof(target));
            if (double.IsNaN(newCondition) || newCondition < 0 || newCondition >= 1)
                throw new ArgumentOutOfRangeException(nameof(newCondition), "A nova condição precisa estar em [0, 1).");

            Cycle = cycle;
            Target = target;
            NewCondition = newCondition;
            Title = title ?? string.Empty;
        }

        /// <summary>Ciclo em que o imprevisto acontece (antes de o ciclo ser simulado).</summary>
        public int Cycle { get; }

        /// <summary>Id do equipamento afetado, ou <see cref="SealTarget"/>.</summary>
        public string Target { get; }

        /// <summary>Condição que a peça passa a ter: 0 = quebrada.</summary>
        public double NewCondition { get; }

        /// <summary>Frase curta para o aviso.</summary>
        public string Title { get; }
    }
}
