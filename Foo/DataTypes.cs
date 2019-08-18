using System;

namespace Foo
{
    public class Post
    {
        public int PostId { get; set; }
        public string Text { get; set; }
        public DateTime CreationDate { get; set; }
    }

    public class Parent
    {
        public int ParentId { get; set; }
    }

    public class Child
    {
        public int ChildId { get; set; }
        public int ParentId { get; set; }
    }
}