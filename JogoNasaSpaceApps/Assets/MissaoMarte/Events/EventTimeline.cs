using System;
using System.Collections.Generic;
using MissaoMarte.Simulation;

namespace MissaoMarte.Events
{
    /// <summary>
    /// Calendário dos eventos da missão: tempestades de poeira e imprevistos com equipamentos.
    /// É determinístico (sem sorteio): a mesma missão se repete igual, o que facilita testar e demonstrar.
    /// </summary>
    public sealed class EventTimeline
    {
        readonly List<DustStorm> storms = new List<DustStorm>();
        readonly List<EquipmentFailure> failures = new List<EquipmentFailure>();

        public EventTimeline(IEnumerable<DustStorm> dustStorms = null, IEnumerable<EquipmentFailure> equipmentFailures = null)
        {
            if (dustStorms != null)
            {
                foreach (DustStorm storm in dustStorms)
                {
                    if (storm == null) throw new ArgumentException("A lista de tempestades contém um item nulo.");
                    storms.Add(storm);
                }
            }

            if (equipmentFailures != null)
            {
                foreach (EquipmentFailure failure in equipmentFailures)
                {
                    if (failure == null) throw new ArgumentException("A lista de imprevistos contém um item nulo.");
                    failures.Add(failure);
                }
            }
        }

        public IReadOnlyList<DustStorm> DustStorms => storms;

        public IReadOnlyList<EquipmentFailure> EquipmentFailures => failures;

        /// <summary>Tempestade ativa no ciclo (a mais forte, se houver sobreposição), ou null.</summary>
        public DustStorm GetActiveStorm(int cycle)
        {
            DustStorm strongest = null;
            foreach (DustStorm storm in storms)
            {
                if (storm.IsActive(cycle) && (strongest == null || storm.SolarFactor < strongest.SolarFactor))
                    strongest = storm;
            }
            return strongest;
        }

        /// <summary>Próxima tempestade que começa depois do ciclo informado, ou null.</summary>
        public DustStorm GetNextStorm(int cycle)
        {
            DustStorm next = null;
            foreach (DustStorm storm in storms)
            {
                if (storm.StartCycle > cycle && (next == null || storm.StartCycle < next.StartCycle))
                    next = storm;
            }
            return next;
        }

        /// <summary>Imprevistos marcados para o ciclo.</summary>
        public IEnumerable<EquipmentFailure> GetFailures(int cycle)
        {
            foreach (EquipmentFailure failure in failures)
            {
                if (failure.Cycle == cycle)
                    yield return failure;
            }
        }

        /// <summary>Condições externas (tempo) que valem para o ciclo.</summary>
        public CycleModifiers GetModifiers(int cycle)
        {
            DustStorm active = GetActiveStorm(cycle);
            return new CycleModifiers { SolarGenerationFactor = active != null ? active.SolarFactor : 1.0 };
        }
    }
}
