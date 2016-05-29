using System.Reflection;

namespace QueryLifting
{
    public class Usage
    {
        public MethodBase CurrentMethod { get; }
        public MemberInfo ResolveMember { get; }

        public Usage(MethodBase currentMethod, MemberInfo resolveMember)
        {
            CurrentMethod = currentMethod;
            ResolveMember = resolveMember;
        }
    }
}