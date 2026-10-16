// Unity(.NET Standard 2.1)에는 이 타입이 없어서 C# 9 record 의 init 접근자가 컴파일되지 않는다.
// 컴파일러는 이름만 보므로 빈 타입을 직접 정의하면 된다.
// (dotnet test 쪽 net 대상에는 이미 있으므로 중복 정의를 피한다.)
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
