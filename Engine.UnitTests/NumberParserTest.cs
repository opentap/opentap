using NUnit.Framework;
using System.Collections.Generic;

namespace OpenTap.UnitTests
{
    internal class NumberParserTest
    {
        [Test]
        public void TokenizerTest()
        {
            List<ExpressionParser.Token> tokens = ExpressionParser.Tokenize(" 5 +\n4 -\t3 * 5+3");
            Assert.That(tokens.Count, Is.EqualTo(9));
            Assert.That(tokens[0].Type, Is.EqualTo(ExpressionParser.TokenType.Number));
            Assert.That(tokens[0].Match, Is.EqualTo("5"));
            Assert.That(tokens[1].Type, Is.EqualTo(ExpressionParser.TokenType.Plus));
            Assert.That(tokens[2].Type, Is.EqualTo(ExpressionParser.TokenType.Number));
            Assert.That(tokens[2].Match, Is.EqualTo("4"));
            Assert.That(tokens[3].Type, Is.EqualTo(ExpressionParser.TokenType.Minus));
            Assert.That(tokens[4].Type, Is.EqualTo(ExpressionParser.TokenType.Number));
            Assert.That(tokens[4].Match, Is.EqualTo("3"));
            Assert.That(tokens[5].Type, Is.EqualTo(ExpressionParser.TokenType.Multiply));
            Assert.That(tokens[6].Type, Is.EqualTo(ExpressionParser.TokenType.Number));
            Assert.That(tokens[6].Match, Is.EqualTo("5"));
            Assert.That(tokens[7].Type, Is.EqualTo(ExpressionParser.TokenType.Plus));
            Assert.That(tokens[8].Type, Is.EqualTo(ExpressionParser.TokenType.Number));
            Assert.That(tokens[8].Match, Is.EqualTo("3"));
        }

        [Test]
        public void AstTest()
        {
            ExpressionParser.IExpressionNode root = ExpressionParser.ParseAst("(5+3)/2");
            Assert.IsInstanceOf<ExpressionParser.DivideNode>(root);
            ExpressionParser.DivideNode divide = root as ExpressionParser.DivideNode;
            
            Assert.IsInstanceOf<ExpressionParser.NumberNode>(divide.Right);
            ExpressionParser.NumberNode number = divide.Right as ExpressionParser.NumberNode;
            Assert.That(number.Value, Is.EqualTo(number.Value));

            Assert.IsInstanceOf<ExpressionParser.PlusNode>(divide.Left);
            ExpressionParser.PlusNode plus = divide.Left as ExpressionParser.PlusNode;

            Assert.IsInstanceOf<ExpressionParser.NumberNode>(plus.Left);
            number = plus.Left as ExpressionParser.NumberNode;
            Assert.That(number.Value, Is.EqualTo(number.Value));

            Assert.IsInstanceOf<ExpressionParser.NumberNode>(plus.Right);
            number = plus.Right as ExpressionParser.NumberNode;
            Assert.That(number.Value, Is.EqualTo(number.Value));
        }

        [TestCase("1+1", 2)]
        [TestCase("5 + 4 - 3 * 5+3", -3)]
        [TestCase("(5+3)/2", 4)]
        [TestCase("5*6/3", 10)]
        public void TestExpressions(string expression, BigFloat expected)
        {
            BigFloat value = ExpressionParser.Calculate(expression);
            Assert.That(value, Is.EqualTo(expected));
        }
    }
}
