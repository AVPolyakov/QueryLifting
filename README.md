# QueryLifting
The [test](Foo.Tests/QueryTests.cs#L33) 
[executes](Foo.Tests/QueryChecker.cs#L31)
the SQL queries in SchemaOnly mode. 
The test checks the match of 
the [names and types of query columns](Foo/Program.cs#L42)
and the [calls of DataReader methods](Foo/Program.cs#L50). 
If there is no match then the test 
[writes to console](Foo.Tests/QueryChecker.cs#L116)
the code for correct data retrieving.

The [general test](Foo.Tests/QueryTests.cs#L33)
[finds](QueryLifting/UsageResolver.cs#L14) and calls 
all the methods where a method of
[specified set](Foo.Tests/QueryTests.cs#L44)
is invoked.
You can explicitly specify
the [test method](Foo.Tests/QueryTests.cs#L60).

Test values of parameters are specified in the method
[TestValues](Foo.Tests/QueryTests.cs#L73).
The test iterates through
[all combinations of test values](QueryLifting/EnumerableExtensions.cs#L10).
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
the [test values for the particular parameter](Foo.Tests/QueryTests.cs#L60).

The [test method](Foo/Program.cs#L61) 
may be [anonymous](QueryLifting/Func.cs#L7)
(code coverage visualization by [dotCover](https://www.jetbrains.com/help/dotcover/10.0/Visualizing_Code_Coverage.html)):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  

## Find All References to database entities: tables, columns, etc.

In addition to the validation of queries the tests can be used to find 
all references to database entities: tables, columns, etc. 
The [method](Foo.Tests/QueryTests.cs#L191) 
writes to the console the locations of the queries that use 
the specified database entity `tableName: "Post", columnName: "CreationDate"`.
```
Program.cs, Ln 97, Col 13
Program.cs, Ln 26, Col 17
Program.cs, Ln 46, Col 17
Program.cs, Ln 56, Col 17
Program.cs, Ln 63, Col 21
Program.cs, Ln 108, Col 71
Program.cs, Ln 125, Col 71
Program.cs, Ln 151, Col 17
Program.cs, Ln 171, Col 17
Program.cs, Ln 180, Col 89
```