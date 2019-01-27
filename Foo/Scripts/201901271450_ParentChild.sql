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

