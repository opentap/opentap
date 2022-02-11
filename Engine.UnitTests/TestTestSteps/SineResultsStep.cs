using System;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Sine Results", "Generates a sine wave of results.", "Tests")]
    public class SineResultsStep : TestStep
    {
        public double Periods { get; set; } = 1;
        public int Samples { get; set; } = 1024;
        public override void Run()
        {
            for (int i = 0; i < Samples; i++)
            {
                var phase = i * (Periods / Samples);
                Results.Publish("Sine", new {Phase = phase, Amplitude = Math.Sin(phase)});
            }
        }
    }
}