using System.Reflection;

namespace QueryLifting
{
    public class Usage
    {
        public MethodBase CurrentMethod { get; }
        public MemberInfo ResolvedMember { get; }

        public Usage(MethodBase currentMethod, MemberInfo resolvedMember)
        {
            CurrentMethod = currentMethod;
            ResolvedMember = resolvedMember;
        }
    }
}