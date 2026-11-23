// Verifica WalkingAndHoldingSprintAgainstWallStaySilent, RunningBreathStartsNearExhaustionAndWalkingRecoversGradually, ScareTriggersBreathingWithoutMovementAndFades, HoldingBreathMutesFearWithoutErasingIt, PausedClockPreservesResponse.

using NUnit.Framework;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    public sealed class BreathingResponseTests
    {
        [Test] public void WalkingAndHoldingSprintAgainstWallStaySilent()
        {
            var response = new BreathingResponse();
            for (int i=0;i<100;i++) response.Step(.1f,false,.5f,0,false);
            Assert.That(response.Demand, Is.Zero);
        }
        [Test] public void RunningBreathStartsNearExhaustionAndWalkingRecoversGradually()
        {
            var response = new BreathingResponse();
            float previous=0;
            for (int i=0;i<72;i++)
            {
                float remaining=1-(i+1)/72f;
                response.Step(.1f,true,remaining,0,false);
                if (remaining>=.3f) Assert.That(response.Demand, Is.Zero, "Normal running must not pant early.");
                else Assert.That(response.Demand, Is.GreaterThan(previous), "Panting must build as stamina runs out.");
                previous=response.Demand;
            }
            float high=response.Demand;
            Assert.That(high, Is.GreaterThan(.9f));
            response.Step(.1f,false,.04f,0,false);
            Assert.That(response.Demand, Is.LessThan(high).And.GreaterThan(.5f));
            for (int i=0;i<90;i++) response.Step(.1f,false,1,0,false);
            Assert.That(response.Demand, Is.Zero);
        }
        [Test] public void ScareTriggersBreathingWithoutMovementAndFades()
        {
            var response=new BreathingResponse(); response.Fright(.8f,4);
            response.Step(.1f,false,1,0,false); Assert.That(response.Demand, Is.GreaterThan(.7f));
            for(int i=0;i<50;i++)response.Step(.1f,false,1,0,false);
            Assert.That(response.Demand, Is.Zero);
        }
        [Test] public void HoldingBreathMutesFearWithoutErasingIt()
        {
            var response=new BreathingResponse(); response.Fright(.8f,5);
            response.Step(.1f,false,1,0,true); Assert.That(response.Demand, Is.Zero);
            response.Step(.1f,false,1,0,false); Assert.That(response.Demand, Is.GreaterThan(.6f));
        }
        [Test] public void PausedClockPreservesResponse()
        {
            var response=new BreathingResponse(); response.Fright(.9f,3);
            response.Step(.1f,false,1,0,false); float before=response.Demand;
            for(int i=0;i<100;i++)response.Step(0,false,1,0,false);
            Assert.That(response.Demand, Is.EqualTo(before));
        }
    }
}
