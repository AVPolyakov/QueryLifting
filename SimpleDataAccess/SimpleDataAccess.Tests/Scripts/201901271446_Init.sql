CREATE TABLE Post (
  PostId INT IDENTITY PRIMARY KEY,
  Text NVARCHAR(MAX) NULL,
  CreationDate DATETIME NOT NULL
);

INSERT INTO Post (Text, CreationDate)
  VALUES ('Test1', '2021-01-14');
INSERT INTO Post (Text, CreationDate)
  VALUES (NULL, '2021-02-15');
