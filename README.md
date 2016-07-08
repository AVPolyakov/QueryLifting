# QueryLifting
Query Lifting is a technique to check SQL queries that are expressed as simple strings.

The [test](Foo.Tests/Tests.cs#L15) covers [code fragments](Foo/Program.cs#L24) that contain queries 
(highlighted by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  
The test [executes](https://msdn.microsoft.com/en-us/library/y6wy5a0f(v=vs.100))
SQL queries in
[SchemaOnly](https://msdn.microsoft.com/en-us/library/system.data.commandbehavior(v=vs.100))
mode.
The test checks match of
[columns names and types in the query](Foo/Program.cs#L26)
and the [properties of C# class](Foo/AnonymousTypes.cs#L8).
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L77)
the [code of class](Foo/AnonymousTypes.cs#L6) 
with correct set of properties.