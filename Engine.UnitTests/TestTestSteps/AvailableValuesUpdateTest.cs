using System;
using System.ComponentModel;
using System.Linq;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    public enum TestEnum
    {
        A,
        B,
        C
    }
    [Display("Available Values Update", "For testing more advanced available values situations.", "Tests")]
    public class AvailableValuesUpdateTest : TestStep
    {
        [AvailableValues(nameof(NotB))] public TestEnum A { get; set; } = TestEnum.A;
        [AvailableValues(nameof(NotA))] public TestEnum B { get; set; } = TestEnum.B;

        public TestEnum[] NotA => Enum.GetValues(typeof(TestEnum)).OfType<TestEnum>().Except(new[] {A}).ToArray();
        public TestEnum[] NotB => Enum.GetValues(typeof(TestEnum)).OfType<TestEnum>().Except(new[] {B}).ToArray();
        
        [AvailableValues(nameof(IncreasingNumbers))]
        public int FromIncreasingNumber { get; set; }

        public int[] IncreasingNumbers => new int[] { IncreasingNumber, IncreasingNumber + 1, IncreasingNumber + 2 };
        public int IncreasingNumber;

        [Browsable(true)]
        public void IncreaseNumber()
        {
            IncreasingNumber += 1;
        } 

        public override void Run()
        {
            
        }
    }
}