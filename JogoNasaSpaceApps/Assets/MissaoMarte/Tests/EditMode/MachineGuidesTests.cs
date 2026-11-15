using System.Linq;
using MissaoMarte.Events;
using MissaoMarte.Game;
using MissaoMarte.Simulation;
using NUnit.Framework;

namespace MissaoMarte.Tests
{
    public class MachineGuidesTests
    {
        static readonly string[] Machines =
        {
            EquipmentIds.OxygenGenerator, EquipmentIds.Co2Scrubber, EquipmentIds.WaterRecycler,
            EquipmentIds.IceProcessor, EquipmentIds.Infrastructure, EquipmentFailure.SealTarget,
            MachineGuides.Lab, MachineGuides.Panels,
        };

        [Test]
        public void TodaMaquinaDaBaseTemTutorialCompleto()
        {
            foreach (string id in Machines)
            {
                MachineGuide guide = MachineGuides.For(id);
                Assert.That(guide, Is.Not.Null, id);
                Assert.That(guide.Id, Is.EqualTo(id));
                foreach (string text in new[] { guide.Name, guide.Question, guide.WhatItDoes, guide.WhyOnMars, guide.RealFact, guide.Source })
                    Assert.That(string.IsNullOrWhiteSpace(text), Is.False, id);
                Assert.That(guide.Question.EndsWith("?"), Is.True, id);
            }
        }

        [Test]
        public void TextosCurtosOBastanteParaATelaDoTutorial()
        {
            foreach (MachineGuide guide in MachineGuides.All)
            {
                Assert.That(guide.WhatItDoes.Length, Is.LessThanOrEqualTo(160), guide.Id);
                Assert.That(guide.WhyOnMars.Length, Is.LessThanOrEqualTo(160), guide.Id);
                Assert.That(guide.RealFact.Length, Is.LessThanOrEqualTo(160), guide.Id);
            }
        }

        [Test]
        public void SemTutorialRepetidoNemParaIdDesconhecido()
        {
            Assert.That(MachineGuides.All.Select(g => g.Id).Distinct().Count(), Is.EqualTo(MachineGuides.All.Count));
            Assert.That(MachineGuides.All.Count, Is.EqualTo(Machines.Length));
            Assert.That(MachineGuides.For("nao_existe"), Is.Null);
            Assert.That(MachineGuides.For(null), Is.Null);
        }
    }
}
