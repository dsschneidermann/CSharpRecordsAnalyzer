// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using CSharpRecordsAnalyzer.Test.Verifiers;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharpRecordsAnalyzer.Test
{
    [TestClass]
    public class CSharpRecordsCodeFixTests : CodeFixVerifier
    {
        [TestMethod]
        public void AttributesAreKeptForCtor()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }

    /// <summary>The Foo</summary>
    [CtorAttribute]
    public Foo(string Bar)
    {
        this.Bar = Bar;
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    /// <summary>The Foo</summary>
    [CtorAttribute]
    public Foo(string Bar)
    {
        this.Bar = Bar;
    }
}
";
            AssertUpdateConstructorTransformsTo(before, after);
        }

        [TestMethod]
        public void AttributesAreKeptForWithMethod()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    [WithMethodAttribute]
    public Foo With(string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    [WithMethodAttribute]
    public Foo With(string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void ExpressionBodiedNotationIsIgnored()
        {
            var before = @"
internal class Foo
{
    public readonly int A;
    public int DeuxA => 2 * A;
}
";
            var after = @"
internal class Foo
{
    public readonly int A;
    public int DeuxA => 2 * A;

    public Foo(int A)
    {
        this.A = A;
    }

    public Foo With(int? A = null)
    {
        return new Foo(A ?? this.A);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void GenericTypeArgsForWithMethod()
        {
            var before = @"
public class Foo<T>
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    public Foo With(string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            var after = @"
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
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void GetWithBodyIsIgnored()
        {
            var before = @"
internal class Foo
{
    public readonly int A;
    public int DeuxA { get { return 2 * A; } }
}
";
            var after = @"
internal class Foo
{
    public readonly int A;
    public int DeuxA { get { return 2 * A; } }

    public Foo(int A)
    {
        this.A = A;
    }

    public Foo With(int? A = null)
    {
        return new Foo(A ?? this.A);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void MultiplePropertiesWithNonNullableType()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
    public DateTime Something { get; }
    public int N { get; }
    public long M { get; }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }
    public DateTime Something { get; }
    public int N { get; }
    public long M { get; }

    public Foo(string Bar, DateTime Something, int N, long M)
    {
        this.Bar = Bar;
        this.Something = Something;
        this.N = N;
        this.M = M;
    }

    public Foo With(string Bar = null, DateTime? Something = null, int? N = null, long? M = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something, N ?? this.N, M ?? this.M);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void MultiplePropertiesWithNullableType()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
    public DateTime? Something { get; }
    public int? N { get; }
    public decimal? Amount { get; }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }
    public DateTime? Something { get; }
    public int? N { get; }
    public decimal? Amount { get; }

    public Foo(string Bar, DateTime? Something, int? N, decimal? Amount)
    {
        this.Bar = Bar;
        this.Something = Something;
        this.N = N;
        this.Amount = Amount;
    }

    public Foo With(string Bar = null, DateTime? Something = null, int? N = null, decimal? Amount = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something, N ?? this.N, Amount ?? this.Amount);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void ParameterAttributesAreKeptForCtor()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }

    public Foo([Jetbrains.Annotations.RegexPattern] string Bar)
    {
        this.Bar = Bar;
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    public Foo([Jetbrains.Annotations.RegexPattern] string Bar)
    {
        this.Bar = Bar;
    }
}
";
            AssertUpdateConstructorTransformsTo(before, after);
        }

        [TestMethod]
        public void ParameterAttributesArePropagatedToWithMethod()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }

    public Foo([Jetbrains.Annotations.RegexPattern] string Bar)
    {
        this.Bar = Bar;
    }

    public Foo With(string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    public Foo([Jetbrains.Annotations.RegexPattern] string Bar)
    {
        this.Bar = Bar;
    }

    public Foo With([Jetbrains.Annotations.RegexPattern] string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void PreviouslyNonNullableAreKept()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }

    public Foo(string Bar, SomeStruct Something)
    {
        this.Bar = Bar;
        this.Something = Something;
    }

    public Foo With(string Bar = null, SomeStruct? Something = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something);
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }

    public Foo(string Bar, SomeStruct Something, int N)
    {
        this.Bar = Bar;
        this.Something = Something;
        this.N = N;
    }

    public Foo With(string Bar = null, SomeStruct? Something = null, int? N = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something, N ?? this.N);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void PropertiesWithNullableType()
        {
            var before = @"
public class Foo
{
    public int? NullableInt { get; }
}
";
            var after = @"
public class Foo
{
    public int? NullableInt { get; }

    public Foo(int? NullableInt)
    {
        this.NullableInt = NullableInt;
    }

    public Foo With(int? NullableInt = null)
    {
        return new Foo(NullableInt ?? this.NullableInt);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void PropertiesWithNullableTypeRepeated()
        {
            var before = @"
public class Foo
{
    public Foo(decimal? N)
    {
        this.N = N;
    }

    public decimal? N { get; }

    public Foo With(decimal? N = null)
    {
        return new Foo(N ?? this.N);
    }
}
";
            var after = @"
public class Foo
{
    public Foo(decimal? N)
    {
        this.N = N;
    }

    public decimal? N { get; }

    public Foo With(decimal? N = null)
    {
        return new Foo(N ?? this.N);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void SimpleSinglePropertyCodeFix()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }

    public Foo With(string Bar = null)
    {
        return new Foo(Bar ?? this.Bar);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void SimpleSinglePropertyConstructorOnly()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }

    public Foo(string Bar)
    {
        this.Bar = Bar;
    }
}
";
            AssertUpdateConstructorTransformsTo(before, after);
        }

        [TestMethod]
        public void StaticFieldsAndMethodsAreLeftUntouched_Ctor()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }
    public static Foo Empty = new Foo("""", SomeStruct.Empty);
    public static int Test { get { return 10; } }
    public static Foo MakeEmpty()
    {
        return new Foo("""", SomeStruct.Empty);
    }

    public Foo(string Bar, SomeStruct Something)
    {
        this.Bar = Bar;
        this.Something = Something;
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }
    public static Foo Empty = new Foo("""", SomeStruct.Empty);
    public static int Test { get { return 10; } }
    public static Foo MakeEmpty()
    {
        return new Foo("""", SomeStruct.Empty);
    }

    public Foo(string Bar, SomeStruct Something, int N)
    {
        this.Bar = Bar;
        this.Something = Something;
        this.N = N;
    }
}
";
            AssertUpdateConstructorTransformsTo(before, after);
        }

        [TestMethod]
        public void StaticFieldsAndMethodsAreLeftUntouched_With()
        {
            var before = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }
    public static Foo Empty = new Foo("""", SomeStruct.Empty);
    public static int Test { get { return 10; } }
    public static Foo MakeEmpty()
    {
        return new Foo("""", SomeStruct.Empty);
    }

    public Foo(string Bar, SomeStruct Something)
    {
        this.Bar = Bar;
        this.Something = Something;
    }

    public Foo With(string Bar = null, SomeStruct? Something = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something);
    }
}
";
            var after = @"
public class Foo
{
    public string Bar { get; }
    public SomeStruct Something { get; }
    public int N { get; }
    public static Foo Empty = new Foo("""", SomeStruct.Empty);
    public static int Test { get { return 10; } }
    public static Foo MakeEmpty()
    {
        return new Foo("""", SomeStruct.Empty);
    }

    public Foo(string Bar, SomeStruct Something, int N)
    {
        this.Bar = Bar;
        this.Something = Something;
        this.N = N;
    }

    public Foo With(string Bar = null, SomeStruct? Something = null, int? N = null)
    {
        return new Foo(Bar ?? this.Bar, Something ?? this.Something, N ?? this.N);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        [TestMethod]
        public void XmlIsKeptForCtor()
        {
            var before = @"
internal class Foo
{
    /// <summary>My A</summary>
    public readonly int A;
    public readonly int B;

    /// <summary>The Ctor</summary>
    /// <param name=""A"">My A</param>
    public Foo(int A)
    {
        this.A = A;
    }
}
";
            var after = @"
internal class Foo
{
    /// <summary>My A</summary>
    public readonly int A;
    public readonly int B;

    /// <summary>The Ctor</summary>
    /// <param name=""A"">My A</param>
    public Foo(int A, int B)
    {
        this.A = A;
        this.B = B;
    }
}
";
            AssertUpdateConstructorTransformsTo(before, after);
        }

        [TestMethod]
        public void XmlIsKeptForWithMethod()
        {
            var before = @"
internal class Foo
{
    /// <summary>My A</summary>
    public readonly int A;
    public readonly int B;

    public Foo(int A)
    {
        this.A = A;
    }

    /// <summary>The With method</summary>
    /// <param name=""A"">My A</param>
    public Foo With(int? A = null)
    {
        return new Foo(A ?? this.A);
    }
}
";
            var after = @"
internal class Foo
{
    /// <summary>My A</summary>
    public readonly int A;
    public readonly int B;

    public Foo(int A, int B)
    {
        this.A = A;
        this.B = B;
    }

    /// <summary>The With method</summary>
    /// <param name=""A"">My A</param>
    public Foo With(int? A = null, int? B = null)
    {
        return new Foo(A ?? this.A, B ?? this.B);
    }
}
";
            AssertUpdateConstructorAndWithMethodTransformsTo(before, after);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new CSharpRecordsCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpRecordsAnalyzer();
        }

        private void AssertUpdateConstructorAndWithMethodTransformsTo(string preCodeFix, string expectedPostCodeFix)
        {
            VerifyCSharpFix(preCodeFix, expectedPostCodeFix, 0);
        }

        private void AssertUpdateConstructorTransformsTo(string preCodeFix, string expectedPostCodeFix)
        {
            VerifyCSharpFix(preCodeFix, expectedPostCodeFix, 1);
        }
    }
}
