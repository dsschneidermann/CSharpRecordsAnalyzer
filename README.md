# CSharpRecordsAnalyzer ![NuGet Version](https://img.shields.io/nuget/v/CSharpRecordsAnalyzer.svg?logo=nuget)

Created from [matzoliv/CSharpRecords](https://github.com/matzoliv/CSharpRecords) (vsix extension) as a starting point and much improved.

### Getting started

Install the [CSharpRecordsAnalyzer](https://nuget.org/packages/CSharpRecordsAnalyzer) package from NuGet:

```shell
dotnet add package CSharpRecordsAnalyzer
```

It should not be necessary to restart Visual Studio, but check if the analyzer is loaded under Dependencies -> Analyzers.

### What does it do?

Adds an analyzer and code fix to make the following class or struct:

```csharp
public class Foo
{
    public string First { get; }
    public int Second { get; }
}
```

Into a record type with a `With` modifier method (as seen in *F#*):

```csharp
public class Foo
{
    public string First { get; }
    public int Second { get; }

    public Foo(string First, int Second)
    {
        this.First = First;
        this.Second = Second;
    }

    public Foo With(string First = null, int? Second = null)
    {
        return new Foo(First ?? this.First, Second ?? this.Second);
    }
}
```

Using the record type is easy:

```csharp
class Program
{
    public Foo Default = new Foo(First: "myFirst", Second: 2);

    public static void Main()
    {
        var myInstance = Default.With(Second: 3);
        
        // Default instance is never changed
        var myOtherInstance = Default.With(First: "myOther"); // Second remains 2
    }
}
```

### Features

* XmlDoc comments propagate from fields to Constructor and `With` method.
* Attributes on Constructor and `With` method are kept, including any Parameter Attributes added.
* Generic record types: `Foo<T>` fully supported.
* Creates a **compiler warning: RecordUpdate** if a record needs to be updated, ie. run the code fix.

The code fix will also work if a class has private fields, static fields and methods of any kind. However only properties and fields are understood to be part of the record.

### Code fix

To use the code fix, hover over the class/struct name in the declaration (eg. `public class Foo`) in Visual Studio and select one of the options:

> Update record constructor and modifier
>
> Update record constructor

If the code fix action does not appear, make sure you have either a `public T PropertyName { get; }` property or a `public readonly T FieldName` field in the class/struct.

### Non-nullable parameters

When generating the `With` method, the code fix will automatically use nullability for predefined types like `int`, `bool`, `Guid`, `DateTimeOffset` and more.

If using a non-nullable type as a field or property, the `With` method will be generated with an obvious *syntax error*. To fix it, just add `?` at the end of the parameter type:

```csharp
public Foo With(string First = null, MyStructType  MyStruct = null)
//                                               ^
//                                   Add '?' here to make it nullable
```

Once added, the nullability will not disappear for that parameter if updated again, it only needs to be fixed the first time.

### XmlDoc comments

The code fix will propagate comments from properties and fields to the Constructor and `With` method:

```csharp
public class Foo
{
    /// <summary>My first string</summary>
    public string First { get; }
    /// <summary>My second int</summary>
    public int Second { get; }
}
```

Creates a Constructor as such:
```csharp
/// <summary>Creates a record instance.</summary>
/// <param name="First">My first string</param>
/// <param name="Second">My second int</param>
public Foo(string First, int Second)
{
    this.First = First;
    this.Second = Second;
}
```

And the same for the `With` method.
