#if UNITY_EDITOR
using System;
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;

namespace RhythmForge.Editor
{
    public class EventBusTests
    {
        [Test]
        public void Publish_InvokesSubscribedHandlers_AndUnsubscribeStopsDelivery()
        {
            var bus = new RhythmForgeEventBus();
            int callCount = 0;
            PatternType lastMode = PatternType.RhythmLoop;

            Action<DrawModeChangedEvent> handler = evt =>
            {
                callCount++;
                lastMode = evt.Mode;
            };

            bus.Subscribe<DrawModeChangedEvent>(handler);
            bus.Publish(new DrawModeChangedEvent(PatternType.MelodyLine));
            bus.Unsubscribe<DrawModeChangedEvent>(handler);
            bus.Publish(new DrawModeChangedEvent(PatternType.HarmonyPad));

            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(lastMode, Is.EqualTo(PatternType.MelodyLine));
        }
    }
}
#endif
