// Domain/IsExternalInit.cs 와 같은 이유. internal 이라 Unity 어셈블리(asmdef)마다 하나씩 필요하다.
// dotnet test 는 모든 소스를 한 어셈블리로 컴파일해서 누락을 못 잡는다 → AssemblyLayoutTests 가 파일 존재로 검사한다.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
