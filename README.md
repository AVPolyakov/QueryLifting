# QueryLifting
The [test](Foo.Tests/QueryTests.cs#L25) 
[executes](Foo.Tests/QueryChecker.cs#L19)
the SQL queries in SchemaOnly mode. 
The test checks match of 
the [names and types of query columns](Foo/Program.cs#L20)
and the [calls of DataReader methods](Foo/Program.cs#L27-L29). 
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L94-L98)
the code for correct data retrieving.

The [general test](Foo.Tests/QueryTests.cs#L25)
[finds](QueryLifting/UsageResolver.cs#L14) and calls 
all the methods where a method of
[specified set](Foo.Tests/QueryTests.cs#L32-L33)
is invoked.
You can explicitly specify
the [test method](Foo.Tests/QueryTests.cs#L138).

Test values of parameters are specified in the method
[TestValues](Foo.Tests/QueryTests.cs#L52-L85).
The test iterates through
[all combinations of test values](QueryLifting/EnumerableExtensions.cs#L9).
You can explicitly specify 
the [test values for the particular parameter](Foo.Tests/QueryTests.cs#L141).

The [test method](Foo/Program.cs#L38-L46) 
may be [anonymous](QueryLifting/Func.cs#L7)
(code coverage visualization by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  