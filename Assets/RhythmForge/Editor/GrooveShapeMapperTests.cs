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
    }
}
#endif
