# QueryLifting
Query Lifting is a technique to test SQL queries that are expressed as simple strings.

The test executes SQL queries in SchemaOnly mode. The test checks the match of names and types of query columns and the calls of DataReader methods. If there is no match then the test writes to console the code for correct data retrieving.

The general test finds and calls all methods where a method of specified set is invoked. You can explicitly specify a test method.

Test values of parameters are specified in the method `TestValues`. The test iterates through all combinations of test values. All possible values are listed for the types `bool`, `enum`, `Nullable<>`, `Option<>`, `Choice<,>`, `Func<>`, etc. If the building of query depends on these types only then all versions of query will be tested.

You can explicitly specify test values for particular parameter.

The test method may be anonymous (code coverage visualization by Jetbrains dotCover):  
![Code coverage](Images/CodeCoverage.png?raw=true "Code coverage")  

## Find All References to database entities: tables, columns, etc.

In addition to validation of queries, the tests can be used to find 
all references to database entities: tables, columns, etc. 
The method `FindUsagesTest` writes to console links to locations of queries that use 
specified database entity `tableName: "Post", columnName: "CreationDate"`.  
![Search result](Images/SearchResult.png?raw=true "Search result")  
