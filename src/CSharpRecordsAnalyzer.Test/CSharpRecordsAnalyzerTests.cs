// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharpRecordsAnalyzer.Test
{
    [TestClass]
    public class CSharpRecordsAnalyzerRefactorTests
    {
        [TestMethod]
        public void AllPublicReadonlyFieldsIsEligible()
        {
            var ast = @"
public class Foo
{
    public readonly string Bar;
    public readonly int Dummy;
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void AllPublicReadonlyPropertiesIsEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; }
    public int Dummy { get; }
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void AnyMutableFieldIsNotEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar;
    public readonly int Dummy;
}";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void AnyMutablePrivatePropertyIsNotEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; private set; }
    public int Dummy { get; }
}";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void AnyMutablePropertyIsNotEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; set; }
    public int Dummy { get; }
}";
            AssertEligible(false, ast);
        }

        public void AssertEligible(bool eligible, string ast)
        {
            var tree = CSharpSyntaxTree.ParseText(ast);

            var root = (CompilationUnitSyntax) tree.GetRoot();

            Assert.AreEqual(
                eligible,
                CSharpRecordsAnalyzer.IsTypeEligible(root.ChildNodes().OfType<TypeDeclarationSyntax>().First())
            );
        }

        [TestMethod]
        public void CSharp6GetBodyShortcutSyntaxIsEligible()
        {
            var ast = @"
public class Foo
{
    public IEnumerable<Guid> Doors => m_externalEntityStateSource.AllEntityGuids;
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void EmptyClassIsNotEligible()
        {
            AssertEligible(false, @"public class Foo { }");
        }

        [TestMethod]
        public void GenericTypeArgsIsEligible()
        {
            var ast = @"
public class Foo<T>
{
    public string Bar { get; }
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void MixOfFieldsAndPropertiesIsEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; }
    public readonly int Dummy;
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void MixOfPublicAndPrivateIsEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; }
    private int Dummy1 = 0;
    private int Dummy2 { get; set; }
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void PrivateFieldWithInitialValueIsNotEligible()
        {
            var ast = @"
public class Foo
{
    private int m_timesEntityQueriesBeforeInitialized = 0;
}";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void PropertiesGetWithBodyIsEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get { return ""foobar""; } }
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void PropertiesNoModifiersIsNotEligible()
        {
            var ast = @"
public class Foo
{
    string Bar { get; }
}";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void StaticFieldsOrPropertiesIsEligible()
        {
            var ast = @"
public class Foo
{
    public string Bar { get; }
    public int Dummy { get; }

    public static int Static1;
    public static int StaticTest2;
}";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void StructIsEligible()
        {
            var ast = @"
public struct Foo
{
    public string Bar { get; }
}
";
            AssertEligible(true, ast);
        }
    }
}
