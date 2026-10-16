// Domain/IsExternalInit.cs 와 같은 이유. internal 이라 어셈블리마다 하나씩 필요하다(Unity 에서는 Content 가 별도 어셈블리).
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
