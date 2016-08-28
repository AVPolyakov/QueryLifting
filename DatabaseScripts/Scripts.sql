IF (OBJECT_ID('Post') IS NOT NULL)
BEGIN
  DROP TABLE Post;
END;

CREATE TABLE Post (
  PostId INT IDENTITY PRIMARY KEY,
  Text NVARCHAR(MAX) NULL,
  CreationDate DATETIME NOT NULL
);

INSERT INTO Post (Text, CreationDate)
  VALUES ('Test1', GETDATE());
INSERT INTO Post (Text, CreationDate)
  VALUES (NULL, GETDATE());

CREATE TABLE T001 (
  C1 DATETIME
);

CREATE TABLE Parent  (
  ParentId INT PRIMARY KEY
);

CREATE TABLE Child  (
  ChildId INT PRIMARY KEY,
  ParentId INT NOT NULL,
  FOREIGN KEY (ParentId) REFERENCES Parent(ParentId)
);

INSERT Parent (ParentId) VALUES (1);
INSERT Parent (ParentId) VALUES (2);

INSERT Child (ChildId, ParentId) VALUES (1, 1);
INSERT Child (ChildId, ParentId) VALUES (2, 1);
INSERT Child (ChildId, ParentId) VALUES (3, 2);
INSERT Child (ChildId, ParentId) VALUES (4, 2);
INSERT Child (ChildId, ParentId) VALUES (5, 2);

ALTER DATABASE QueryLifting SET ALLOW_SNAPSHOT_ISOLATION ON;
