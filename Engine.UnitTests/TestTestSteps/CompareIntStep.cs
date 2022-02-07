namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Compare Int", "Compares two integer values and fails if they are different.", "Tests")]
    public class CompareIntStep : TestStep
    {
        public int InputValue { get; set; }
        
        public int ExpectedValue { get; set; }

        public override void Run()
        {
            if (InputValue != ExpectedValue)
            {
                Log.Info($"Compare int: {InputValue} != {ExpectedValue}");
                UpgradeVerdict(Verdict.Fail);
            }
            else
            {
                Log.Info($"Compare int: {InputValue} == {ExpectedValue}");
                UpgradeVerdict(Verdict.Pass);
            }
        }
    }
}