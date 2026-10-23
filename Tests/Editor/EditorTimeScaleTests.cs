using Bomome.EditorTools.TimeControls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools.Tests
{
    public sealed class EditorTimeScaleTests
    {
        [Test]
        public void EditModeCommandsLeaveTimeAndPhysicsUntouched()
        {
            Assert.IsFalse(EditorApplication.isPlaying);
            float scale = Time.timeScale;
            float fixedStep = Time.fixedDeltaTime;
            EditorTimeScale.Set(5f);
            Assert.AreEqual(scale, Time.timeScale);
            Assert.AreEqual(fixedStep, Time.fixedDeltaTime);
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(1f, 1f)]
        [TestCase(5f, 5f)]
        [TestCase(10f, 5f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(float.NegativeInfinity, 1f)]
        public void ControlsOnlyAcceptFiniteValuesInRange(float input, float expected)
        {
            Assert.AreEqual(expected, EditorTimeScale.Normalize(input));
        }

        [TestCase(0f, "0x")]
        [TestCase(0.25f, "0.25x")]
        [TestCase(1f, "1x")]
        [TestCase(5f, "5x")]
        public void LabelShowsCurrentMultiplier(float value, string expected)
        {
            Assert.AreEqual(expected, EditorTimeScale.Format(value));
        }
    }
}
