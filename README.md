# QueryLifting
Query Lifting is a technique to test SQL queries that are expressed as simple strings.

The [test](Foo.Tests/QueryTests.cs#L25) covers [code fragments](Foo/Program.cs#L18-L26) that contain queries 
(highlighted in green by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  
The test
[finds](QueryLifting/UsageResolver.cs#L14)
all the methods where a method of
[specified set](Foo.Tests/QueryTests.cs#L32-L33)
is invoked.
Test values of parameters are specified in the method
[TestValues](Foo.Tests/QueryTests.cs#L50).
The test iterates through 
[all combinations of test values](QueryLifting/EnumerableExtensions.cs#L9).
The test [executes](Foo.Tests/QueryChecker.cs#L19)
SQL queries in SchemaOnly mode.
The test checks match of
[columns names and types in the query](Foo/Program.cs#L20)
and the [properties of C# class](Foo/AnonymousTypes.cs#L8-L10).
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L84-L88)
the [code of class](Foo/AnonymousTypes.cs#L6-L11) 
with correct set of properties.

You can explicitly specify
the [method with query](Foo.Tests/QueryTests.cs#L138)
and the [test values for the particular parameter](Foo.Tests/QueryTests.cs#L139).