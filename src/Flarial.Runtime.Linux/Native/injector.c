/*
 * Derived from BedrockOnLinux src/injector.c
 * Copyright (c) 2026 BedrockOnLinux contributors
 * SPDX-License-Identifier: MIT
 * (full MIT text: src/Flarial.Runtime.Linux/Native/LICENSE-BedrockOnLinux)
 * Modified: loads a list of libraries (dependencies first) and takes the process name first.
 *
 * Build (msvc-wine): see build-injector.sh in this directory.
 */
/* bol injector — load a client .dll into the running Minecraft via the classic
 * CreateRemoteThread + LoadLibraryW technique, so BedrockOnLinux can inject
 * directly without any third-party injector .exe. Built for Wine (x86_64) and
 * run inside the game's own Wine prefix so it shares the wineserver and can see
 * Minecraft.Windows.exe -- by snapshot name, or by PEB image path when Wine
 * reports no name for it. Returns 0 on success.
 *
 * Build: x86_64-w64-mingw32-gcc -O2 -municode -s injector.c -o ../bol/injector.exe
 * Usage: injector.exe <process.exe> <dll> [<dll>...]   (dependencies first, the modification last)
 */
#include <windows.h>
#include <winternl.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <wchar.h>

typedef NTSTATUS (WINAPI *NtQueryInformationProcess_t)(HANDLE, PROCESSINFOCLASS,
                                                       PVOID, ULONG, PULONG);

/* Read a process's image path out of its own PEB. Wine reports an empty
 * szExeFile for the game started by the GDK loader, so the snapshot name alone
 * never matches Minecraft.Windows.exe; the PEB still holds the real path. */
static BOOL peb_image_name(HANDLE proc, wchar_t *out, size_t cap)
{
    static NtQueryInformationProcess_t query;
    PROCESS_BASIC_INFORMATION pbi;
    RTL_USER_PROCESS_PARAMETERS params;
    PEB peb;
    ULONG got = 0;
    size_t n;

    if (!query) {
        query = (NtQueryInformationProcess_t)(void *)GetProcAddress(
            GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationProcess");
        if (!query) return FALSE;
    }
    if (query(proc, ProcessBasicInformation, &pbi, sizeof(pbi), &got)) return FALSE;
    if (!ReadProcessMemory(proc, pbi.PebBaseAddress, &peb, sizeof(peb), NULL))
        return FALSE;
    if (!ReadProcessMemory(proc, peb.ProcessParameters, &params, sizeof(params), NULL))
        return FALSE;

    n = params.ImagePathName.Length / sizeof(wchar_t);
    if (n >= cap) n = cap - 1;
    if (!ReadProcessMemory(proc, params.ImagePathName.Buffer, out,
                           n * sizeof(wchar_t), NULL))
        return FALSE;
    out[n] = 0;
    return TRUE;
}

static const wchar_t *base_name(const wchar_t *path)
{
    const wchar_t *base = path, *p;
    for (p = path; *p; ++p)
        if (*p == L'\\' || *p == L'/') base = p + 1;
    return base;
}

static DWORD find_pid(const wchar_t *name)
{
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;
    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(pe);
    DWORD pid = 0;
    if (Process32FirstW(snap, &pe))
        do {
            if (!_wcsicmp(pe.szExeFile, name)) { pid = pe.th32ProcessID; break; }
            if (pe.szExeFile[0]) continue;   /* named, just not the one we want */

            HANDLE proc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
                                      FALSE, pe.th32ProcessID);
            if (!proc) continue;
            wchar_t image[MAX_PATH];
            if (peb_image_name(proc, image, MAX_PATH) &&
                !_wcsicmp(base_name(image), name))
                pid = pe.th32ProcessID;
            CloseHandle(proc);
            if (pid) break;
        } while (Process32NextW(snap, &pe));
    CloseHandle(snap);
    return pid;
}

/* Loads one library into the target through LoadLibraryW on a remote thread.
 * Returns 0 on success or the exit code to report. */
static int load_library(HANDLE h, const wchar_t *dll)
{
    SIZE_T n = (wcslen(dll) + 1) * sizeof(wchar_t);
    void *rem = VirtualAllocEx(h, NULL, n, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!rem) {
        fwprintf(stderr, L"ERR allocate process memory: %lu\n", GetLastError());
        return 5;
    }
    if (!WriteProcessMemory(h, rem, dll, n, NULL)) {
        fwprintf(stderr, L"ERR write process memory: %lu\n", GetLastError());
        VirtualFreeEx(h, rem, 0, MEM_RELEASE);
        return 5;
    }

    /* kernel32 is mapped at the same address in every Wine process, so the
     * target can call our LoadLibraryW pointer directly. */
    HMODULE k32 = GetModuleHandleW(L"kernel32.dll");
    FARPROC loadlib = GetProcAddress(k32, "LoadLibraryW");
    HANDLE th = CreateRemoteThread(h, NULL, 0, (LPTHREAD_START_ROUTINE)loadlib,
                                   rem, 0, NULL);
    if (!th) {
        fwprintf(stderr, L"ERR CreateRemoteThread: %lu\n", GetLastError());
        VirtualFreeEx(h, rem, 0, MEM_RELEASE);
        return 6;
    }

    DWORD wait = WaitForSingleObject(th, 15000);
    if (wait != WAIT_OBJECT_0) {
        if (wait == WAIT_TIMEOUT)
            fwprintf(stderr, L"ERR LoadLibrary thread timed out\n");
        else
            fwprintf(stderr, L"ERR waiting for LoadLibrary thread: %lu\n",
                     GetLastError());
        /*
         * The remote thread can still be reading the path after a timeout.
         * Do not free that allocation and create a use-after-free in the
         * target; Wine will reclaim the tiny buffer when Minecraft exits.
         */
        CloseHandle(th);
        return 8;
    }
    DWORD mod = 0;                       /* low 32 bits of the loaded HMODULE */
    if (!GetExitCodeThread(th, &mod)) {
        fwprintf(stderr, L"ERR GetExitCodeThread: %lu\n", GetLastError());
        VirtualFreeEx(h, rem, 0, MEM_RELEASE);
        CloseHandle(th);
        return 9;
    }
    VirtualFreeEx(h, rem, 0, MEM_RELEASE);
    CloseHandle(th);

    if (!mod) {
        fwprintf(stderr, L"ERR LoadLibrary returned 0 (bad DLL / 32-bit / missing "
                         L"deps): %ls\n", dll);
        return 7;
    }
    return 0;
}

/* Usage: injector.exe <process.exe> <dll> [<dll> ...]
 * The libraries are loaded in order: every one but the last is a dependency of
 * the last (a failure there is only reported), the last is the actual
 * modification and decides the exit code. */
int wmain(int argc, wchar_t **argv)
{
    if (argc < 3) { fwprintf(stderr, L"usage: injector <process.exe> <dll> [<dll>...]\n"); return 2; }
    const wchar_t *proc = argv[1];

    DWORD pid = find_pid(proc);
    if (!pid) { fwprintf(stderr, L"ERR process not found: %ls\n", proc); return 3; }

    HANDLE h = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION |
                           PROCESS_VM_WRITE | PROCESS_VM_READ |
                           PROCESS_QUERY_INFORMATION, FALSE, pid);
    if (!h) { fwprintf(stderr, L"ERR OpenProcess: %lu\n", GetLastError()); return 4; }

    int last = argc - 1;
    for (int i = 2; i < last; ++i) {
        int dep = load_library(h, argv[i]);
        if (dep) fwprintf(stderr, L"WARN dependency not loaded (%d): %ls\n", dep, argv[i]);
    }

    int code = load_library(h, argv[last]);
    CloseHandle(h);
    if (code) return code;

    fwprintf(stderr, L"OK injected %ls into %ls (pid %lu)\n", argv[last], proc, pid);
    return 0;
}
