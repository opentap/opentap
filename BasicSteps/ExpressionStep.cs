using System;
using System.ComponentModel;
using System.Linq;

namespace OpenTap.Plugins.BasicSteps
{
    using Expr = System.Linq.Expressions.Expression;

    /// <summary> This expression step defines an expression over user defined variables. The expression step defines a
    /// little mini-language for evaluating calculator-like expressions. </summary>
    [Display("Expression", "Invokes a simple expression based on it's variable.", Group: "Basic Steps")]
    [AllowAnyChild]
    public class ExpressionStep : TestStep
    {
        public ExpressionStep()
        {
            Name = "{Mode} {Expression}";
        }
        [DefaultValue("")]
        public string Expression { get; set; } = "";

        /// <summary>
        /// The various modes available for the expression step.
        /// </summary>
        public enum ModeType
        {
            /// <summary> The expression gets evaluated. </summary>
            [Display("Evaluate",  "The expression gets evaluated.")]
            Evaluate,
            /// <summary> Conditional expression. </summary>
            [Display("If",  "If the expression evaluates to 'true', then child steps are executed.")]
            If,
            /// <summary> Set the verdict based on the result of the expression. </summary>
            [Display("Check",  "If the expression evaluates to 'true', then child steps are executed.")]
            Check
        }
        
        /// <summary> When ModeType.If is used, what should happen? </summary>
        public enum IfBehaviorType
        {
            /// <summary> Run the child steps. </summary>
            [Display("Run Child Test Steps", "Run the child test steps.")]
            RunChildTestSteps,
            /// <summary> Break out of the current loop (parent).</summary>
            [Display("Break Loop", "Break the currently executed loop parent step.")]
            BreakLoop
        }

        /// <summary> Gets or sets the current mode for the expression step. </summary>
        [DefaultValue(ModeType.Evaluate)]
        public ModeType Mode { get; set; } = ModeType.Evaluate;

        /// <summary> Gets or sets the behavior in the 'if' mode. </summary>
        [DefaultValue(IfBehaviorType.RunChildTestSteps)]
        [EnabledIf(nameof(Mode), ModeType.If, HideIfDisabled = true)]
        public IfBehaviorType IfBehavior { get; set; } = IfBehaviorType.RunChildTestSteps;
        static readonly ExpressionCodeBuilder codeBuilder = new ExpressionCodeBuilder();
        public override void Run()
        {
            var members = TypeData.GetTypeData(this).GetMembers()
                .OfType<UserDefinedDynamicMember>()
                .ToArray();
            
            // parameters for the expression are the member variables.
            var parameters = members
                .Select(x => Expr.Parameter(x.TypeDescriptor.AsTypeData().Type, x.Name))
                .ToArray();
            
            // parse the expression and get an Abstract Syntax Tree.
            ReadOnlySpan<char> toParse = Expression.ToArray();
            var ast = codeBuilder.Parse(ref toParse);
            
            // generate the expression tree.
            var expr = codeBuilder.GenerateCode(ast, parameters);
            
            // if the mode is Evaluate, then generate the code for returning the value of
            // all the parameters (as an object[]), so if needed, they can be updated.
            if(Mode == ModeType.Evaluate)
                expr = Expr.Block(expr, Expr.NewArrayInit(typeof(object), 
                    parameters.Select(x => Expr.Convert(x, typeof(object)))));
            
            // compile the expression tree as a lambda expression.
            var lmb = Expr.Lambda(expr, false, parameters);
            var d= lmb.Compile();
            
            // invoke it dynamically.
            var result = d.DynamicInvoke(members.Select(x => x.GetValue(this))
                .ToArray());

            if (Mode == ModeType.Evaluate)
            {
                // if it is Evaluation mode, set the values according to what is evaluated.
                // e.g A = X
                var newValues = (object[]) result;
                foreach (var set in newValues.Pairwise(members))
                {
                    set.Item2.SetValue(this, set.Item1);
                }
            }
            else if (Mode == ModeType.If)
            {
                // for If mode, we expect something like X > 3.
                if (Equals(result, true))
                {
                    if (IfBehavior == IfBehaviorType.RunChildTestSteps)
                        RunChildSteps();
                    else if (IfBehavior == IfBehaviorType.BreakLoop)
                        GetParent<LoopTestStep>()?.BreakLoop();
                }else if (!Equals(result, false))
                {
                    throw new Exception("Result of expression is not true/false.");
                }
            }
            else if (Mode == ModeType.Check)
            {
                // this is the same as If, except setting the verdict based on the result.
                if (Equals(result, true))
                    UpgradeVerdict(Verdict.Pass);
                else if(Equals(result, false))
                    UpgradeVerdict(Verdict.Fail);
                else
                    throw new Exception("Result of expression is not true/false.");
            }
        }
    }
}