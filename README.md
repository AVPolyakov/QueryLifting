# QueryLifting
The [test](Foo.Tests/QueryTests.cs#L25) 
[executes](Foo.Tests/QueryChecker.cs#L19)
the SQL queries in SchemaOnly mode. 
The test checks the match of 
the [names and types of query columns](Foo/Program.cs#L20)
and the [calls of DataReader methods](Foo/Program.cs#L27-L29). 
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L92-L96)
the code for correct data retrieving.

The [general test](Foo.Tests/QueryTests.cs#L25)
[finds](QueryLifting/UsageResolver.cs#L14) and calls 
all the methods where a method of
[specified set](Foo.Tests/QueryTests.cs#L32-L33)
is invoked.
You can explicitly specify
the [test method](Foo.Tests/QueryTests.cs#L156).

Test values of parameters are specified in the method
[TestValues](Foo.Tests/QueryTests.cs#L52-L108).
The test iterates through
[all combinations of test values](QueryLifting/EnumerableExtensions.cs#L9).
All possible values are listed for the types 
[bool](https://msdn.microsoft.com/en-us/library/system.boolean(v=vs.110).aspx), 
[enum](https://msdn.microsoft.com/en-us/library/sbbt4032.aspx), 
[Nullable<>](https://msdn.microsoft.com/en-us/library/b3h38hb0(v=vs.110).aspx), 
[Option<>](QueryLifting/Option.cs#L11), 
[Choice<,>](QueryLifting/Choice.cs#L5), 
[Func<>](https://msdn.microsoft.com/en-us/library/bb534960(v=vs.110).aspx), 
etc.
If the building of the query depends on these types only then all versions of the query will be tested.

You can explicitly specify 
the [test values for the particular parameter](Foo.Tests/QueryTests.cs#L158).

The [test method](Foo/Program.cs#L38-L46) 
may be [anonymous](QueryLifting/Func.cs#L7)
(code coverage visualization by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  