#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class GrooveShapeMapperTests
    {
        [Test]
        public void MonotonicDensity_InPathLengthInput()
        {
            var sparse = GrooveShapeMapper.Map(new ShapeProfile { pathLength = 0.1f });
            var medium = GrooveShapeMapper.Map(new ShapeProfile { pathLength = 0.5f });
            var busy = GrooveShapeMapper.Map(new ShapeProfile { pathLength = 0.9f });

            Assert.That(sparse.density, Is.LessThan(medium.density));
            Assert.That(medium.density, Is.LessThan(busy.density));
            Assert.That(sparse.density, Is.InRange(0.5f, 1.5f));
            Assert.That(busy.density, Is.InRange(0.5f, 1.5f));
        }

        [Test]
        public void AccentAmplitude_ScalesWithVerticalSpan()
        {
            var flat = GrooveShapeMapper.Map(new ShapeProfile { verticalSpan = 0.1f });
            var tall = GrooveShapeMapper.Map(new ShapeProfile { verticalSpan = 0.9f });

            Assert.That(flat.accentCurve, Is.Not.Null);
            Assert.That(tall.accentCurve, Is.Not.Null);
            Assert.That(flat.accentCurve[0], Is.EqualTo(1f));
            Assert.That(tall.accentCurve[0], Is.EqualTo(1f));

            float flatContrast = flat.accentCurve[0] - flat.accentCurve[1];
            float tallContrast = tall.accentCurve[0] - tall.accentCurve[1];

            Assert.That(tallContrast, Is.GreaterThan(flatContrast));
            Assert.That(tall.accentCurve[2], Is.LessThan(flat.accentCurve[2]));
        }
    }
}
#endif
