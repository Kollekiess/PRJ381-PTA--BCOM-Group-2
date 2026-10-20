using NUnit.Framework;

namespace BCSignVR.Tests
{
    public sealed class GestureEvaluatorTests
    {
        [Test]
        public void ExactTemplateHasFullConfidence()
        {
            var evaluator = new GestureEvaluator();
            var pose = SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open);
            evaluator.AddTemplate("OPEN", pose);
            var score = evaluator.Evaluate(pose);
            Assert.AreEqual("OPEN", score.label);
            Assert.Greater(score.confidence, .999f);
        }

        [Test]
        public void DistinctDemoPosesClassifyCorrectly()
        {
            var evaluator = new GestureEvaluator();
            evaluator.AddTemplate("OPEN", SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Open));
            evaluator.AddTemplate("FIST", SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Fist));
            evaluator.AddTemplate("POINT", SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Point));
            var score = evaluator.Evaluate(SimulatedHandPoseProvider.Template(SimulatedHandPoseProvider.DemoPose.Point));
            Assert.AreEqual("POINT", score.label);
            Assert.Greater(score.confidence, .999f);
        }
    }
}

