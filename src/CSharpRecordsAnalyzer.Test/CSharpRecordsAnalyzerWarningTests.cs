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
    public class CSharpRecordsAnalyzerWarningTests
    {
        [TestMethod]
        public void AlreadyImmutableCtorIsNotEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar, int Dummy)
{
    this.Bar = Bar;
    this.Dummy = Dummy;
}
}
";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void AlreadyImmutableWithMethodIsNotEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar, int Dummy)
{
    this.Bar = Bar;
    this.Dummy = Dummy;
}

public Foo With(string Bar = null, int? Dummy = null)
{
    return new Foo(Bar ?? this.Bar, Dummy ?? this.Dummy);
}
}
";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void BrokenStructCtorIsEligible()
        {
            var ast = @"
public struct Foo
{
public string Bar { get; }
public int Dummy;

public Foo(string Bar)
{
    this.Bar = Bar;
    // Struct is broken because Dummy must be assigned.
}
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void GenericTypeArgsWithMethodIsEligible()
        {
            var ast = @"
public class Foo<T1, T2>
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    public Foo<T> With(string Bar = null)
    {
        return new Foo<T>(Bar ?? this.Bar);
    }
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void GenericTypeArgsWithMethodIsNotEligible()
        {
            var ast = @"
public class Foo<T>
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    public Foo<T> With(string Bar = null)
    {
        return new Foo<T>(Bar ?? this.Bar);
    }
}
";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void IncorrectImmutableCtorImplementationIsEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar, int Dummy)
{
    this.Bar = Bar;
    //this.Dummy = Dummy;
}
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void IncorrectImmutableCtorParameterIsEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar)
{
    this.Bar = Bar;
}
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void IncorrectImmutableWithMethodImplementationIsEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar, int Dummy)
{
    this.Bar = Bar;
    this.Dummy = Dummy;
}

public Foo(string Bar)
{
    this.Bar = Bar;
}

public Foo With(string Bar = null, int Dummy = null)
{
    return new Foo(Bar ?? this.Bar);
}
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void IncorrectImmutableWithMethodParameterIsEligible()
        {
            var ast = @"
public class Foo
{
public string Bar { get; }
public readonly int Dummy;

public Foo(string Bar, int Dummy)
{
    this.Bar = Bar;
    this.Dummy = Dummy;
}

public Foo With(string Bar = null)
{
    return new Foo(Bar ?? this.Bar);
}
}
";
            AssertEligible(true, ast);
        }

        [TestMethod]
        public void LowerCaseCtorParametersIsNotEligible()
        {
            var ast = @"
public class Foo
{
    public Foo(int n, int i)
    {
    }

    public int N { get; }
}";
            AssertEligible(false, ast);
        }

        [TestMethod]
        public void MixedCaseCtorParametersIsEligible()
        {
            var ast = @"
public class Foo
{
    public Foo(int n, double Val1)
    {
        this.N = n;
        this.Val1 = Val1;
    }

    public int N { get; }
    public double Val1 { get; }
    public double Val2 { get; }
}";
            AssertEligible(true, ast);
        }

        private static void AssertEligible(bool eligible, string ast)
        {
            var tree = CSharpSyntaxTree.ParseText(ast);

            var root = (CompilationUnitSyntax) tree.GetRoot();

            Assert.AreEqual(
                eligible,
                CSharpRecordsAnalyzer.IsImplementedWrong(root.ChildNodes().OfType<TypeDeclarationSyntax>().First())
            );
        }
    }
}
