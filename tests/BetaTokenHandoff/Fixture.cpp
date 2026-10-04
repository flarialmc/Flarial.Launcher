#include <Windows.h>

struct TokenHandoff {
    LONG ready;
    ULONG length;
    char token[4096];
};

extern "C" {
__declspec(dllexport) TokenHandoff FlarialBetaAccessTokenHandoff{};
}
