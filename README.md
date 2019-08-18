# QueryLifting
Query Lifting is a technique to test SQL queries that are expressed as simple strings.

The test executes SQL queries in SchemaOnly mode. The test checks the match of query columns and the properties of data object. If there is no match then the test writes to console the code for correct data retrieving.

The general test finds and calls all methods where a method of specified set is invoked.

## Find All References to database entities: tables, columns, etc.

In addition to validation of queries, the tests can be used to find 
all references to database entities: tables, columns, etc. 
![Search result](Images/SearchResult.png?raw=true "Search result")  
