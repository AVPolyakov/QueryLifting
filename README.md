# QueryLifting
The test [executes](Foo.Tests/QueryChecker.cs#L19)
the SQL queries in SchemaOnly mode.
The test checks match of
[columns names and types in the query](Foo/Program.cs#L20)
and the [properties of C# class](Foo/AnonymousTypes.cs#L8-L10).
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L84-L88)
the [code of class](Foo/AnonymousTypes.cs#L6-L11) 
with correct set of properties.

The [general test](Foo.Tests/QueryTests.cs#L25)
[finds](QueryLifting/UsageResolver.cs#L14) and calls 
all the methods where a method of
[specified set](Foo.Tests/QueryTests.cs#L32-L33)
is invoked.
You can explicitly specify
the [test method](Foo.Tests/QueryTests.cs#L137).

Test values of parameters are specified in the method
[TestValues](Foo.Tests/QueryTests.cs#L52-L85).
The test iterates through
[all combinations of test values](QueryLifting/EnumerableExtensions.cs#L9).
You can explicitly specify 
the [test values for the particular parameter](Foo.Tests/QueryTests.cs#L138).

The [test method](Foo/Program.cs#L18-L26) 
may be [anonymous](QueryLifting/Func.cs#L7)
(code coverage visualization by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  