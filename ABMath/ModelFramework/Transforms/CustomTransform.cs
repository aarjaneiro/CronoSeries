using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;
using CronoSeries.ABMath.ModelFramework.Data;
using Microsoft.CSharp;

namespace CronoSeries.ABMath.ModelFramework.Transforms
{
    [Serializable]
    public class CustomTransform : TimeSeriesTransformation
    {
        [NonSerialized] private MethodInfo methodInfo;
        [NonSerialized] private string methodInfoString;

        public CustomTransform()
        {
            Expression = "x";
        }

        [Category("Parameter")]
        [Description("Mathematical expression as a function of x,y and z.")]
        public string Expression { get; set; }

        [Category("Parameter")] [Description("Number of inputs (1 to 3).")] public int NumInputVariables { get; set; }

        public override int NumInputs()
        {
            if (NumInputVariables < 1)
                NumInputVariables = 1;
            if (NumInputVariables > 3)
                NumInputVariables = 3;
            return NumInputVariables;
        }

        public override int NumOutputs()
        {
            return 1;
        }

        public override string GetInputName(int index)
        {
            return "Input";
        }

        public override string GetOutputName(int index)
        {
            return "Output";
        }

        public override string GetDescription()
        {
            return "Custom";
        }

        public override string GetShortDescription()
        {
            return "Custom";
        }

        //public override Icon GetIcon()
        //{
        //    return null;
        //}

        private void CompileStuff()
        {
            var pro = new CSharpCodeProvider();
            var unit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace("CustomNamespace");

            var classDecl = new CodeTypeDeclaration
            {
                Name = "CustomClass",
                IsClass = true,
                Attributes = MemberAttributes.Static
            };

            var parm1 = new CodeParameterDeclarationExpression(typeof(double), "x");
            var parm2 = new CodeParameterDeclarationExpression(typeof(double), "y");
            var parm3 = new CodeParameterDeclarationExpression(typeof(double), "z");
            var method1 = new CodeMemberMethod
            {
                Name = "MyExp",
                Attributes = MemberAttributes.Static | MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(double)),
                Parameters = {parm1, parm2, parm3},
                Statements = {new CodeMethodReturnStatement(new CodeSnippetExpression(Expression))}
            };
            classDecl.Members.Add(method1);

            var arparm1 = new CodeParameterDeclarationExpression(typeof(double[]), "x");
            var arparm2 = new CodeParameterDeclarationExpression(typeof(double[]), "y");
            var arparm3 = new CodeParameterDeclarationExpression(typeof(double[]), "z");
            var method2 = new CodeMemberMethod
            {
                Name = "ApplyTo",
                Attributes = MemberAttributes.Static | MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(void)),
                Parameters = {arparm1, arparm2, arparm3}
            };

            // Creates a for loop that sets testInt to 0 and continues incrementing testInt by 1 each loop until testInt is not less than 10.
            var testInt = new CodeVariableDeclarationStatement(typeof(int), "testInt", new CodePrimitiveExpression(0));
            var forLoop = new CodeIterationStatement(
                // initStatement parameter for pre-loop initialization.
                new CodeAssignStatement(new CodeVariableReferenceExpression("testInt"), new CodePrimitiveExpression(0)),
                // testExpression parameter to test for continuation condition.
                new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("testInt"),
                    CodeBinaryOperatorType.LessThan, new CodeSnippetExpression("x.Length")),
                // incrementStatement parameter indicates statement to execute after each iteration.
                new CodeAssignStatement(new CodeVariableReferenceExpression("testInt"),
                    new CodeBinaryOperatorExpression(
                        new CodeVariableReferenceExpression("testInt"), CodeBinaryOperatorType.Add,
                        new CodePrimitiveExpression(1))),
                // statements parameter contains the statements to execute during each interation of the loop.
                // Each loop iteration the value of the integer is output using the Console.WriteLine method.
                new CodeExpressionStatement(
                    new CodeSnippetExpression("x[testInt] = MyExp(x[testInt],y[testInt],z[testInt])")));
            method2.Statements.Add(testInt);
            method2.Statements.Add(forLoop);
            classDecl.Members.Add(method2);

            codeNamespace.Types.Add(classDecl);
            unit.Namespaces.Add(codeNamespace);

            // Uncomment the following if you want to debug and see the code that is generated and compiled:
            //var sb = new StringBuilder(1024);
            //var swriter = new StringWriter(sb);
            //pro.CreateGenerator().GenerateCodeFromCompileUnit(unit, swriter, new CodeGeneratorOptions());

            var dom = pro.CompileAssemblyFromDom(new CompilerParameters(), unit);
            if (dom.Errors.Count > 0)
                return;

            var assembly = dom.CompiledAssembly;
            var type = assembly.GetType("CustomNamespace.CustomClass");
            methodInfo = type.GetMethod("ApplyTo");
            methodInfoString = Expression;
        }


        public override void Recompute()
        {
            IsValid = false;
            if (string.IsNullOrEmpty(Expression))
                return;
            if (methodInfo == null || methodInfoString == null || methodInfoString != Expression)
                CompileStuff();
            if (methodInfo == null)
            {
                Console.WriteLine("Unable to parse expression.");
                return;
            }

            var tsList = GetInputBundle();

            outputs = new List<TimeSeries>();

            {
                var tempxArray = new double[tsList[0].Count];
                var tempyArray = new double[tsList[0].Count];
                var tempzArray = new double[tsList[0].Count];

                for (var t = 0; t < tsList[0].Count; ++t)
                {
                    tempxArray[t] = tsList[0][t];
                    if (tsList.Count > 1)
                        tempyArray[t] = tsList[1].ValueAtTime(tsList[0].TimeStamp(t));
                    if (tsList.Count > 2)
                        tempzArray[t] = tsList[2].ValueAtTime(tsList[0].TimeStamp(t));
                }

                methodInfo.Invoke(null, new object[] {tempxArray, tempyArray, tempzArray});

                var newTS = new TimeSeries();
                for (var t = 0; t < tsList[0].Count; ++t)
                    newTS.Add(tsList[0].TimeStamp(t), tempxArray[t], false);
                outputs.Add(newTS);
            }

            IsValid = true;
        }

        public override List<Type> GetAllowedInputTypesFor(int socket)
        {
            if (socket >= NumInputs())
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries)};
        }

        public override List<Type> GetOutputTypesFor(int socket)
        {
            if (socket != 0)
                throw new SocketException();
            return new List<Type> {typeof(TimeSeries)};
        }
    }
}