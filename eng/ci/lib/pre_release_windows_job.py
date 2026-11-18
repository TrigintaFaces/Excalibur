# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Run an owned Windows process tree without a spawn-before-assignment race.

Create the process suspended, assign a non-breakaway kill-on-close Job Object,
then resume its initial thread. Failure to establish ownership refuses execution;
there is no fallback to a broad process-name kill or a racy parent-only timeout.
"""
import ctypes
from ctypes import wintypes as w
import math
import os
import subprocess
import time


def run_owned_windows(argv, cwd, environment, timeout, log):
    import msvcrt

    if len({key.upper() for key in environment}) != len(environment):
        raise OSError('The native Windows environment contains case-duplicate names.')
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    size_t = ctypes.c_size_t

    class BasicLimits(ctypes.Structure):
        _fields_ = [('PerProcessUserTimeLimit', ctypes.c_longlong), ('PerJobUserTimeLimit', ctypes.c_longlong),
                    ('LimitFlags', w.DWORD), ('MinimumWorkingSetSize', size_t), ('MaximumWorkingSetSize', size_t),
                    ('ActiveProcessLimit', w.DWORD), ('Affinity', size_t),
                    ('PriorityClass', w.DWORD), ('SchedulingClass', w.DWORD)]

    class IoCounters(ctypes.Structure):
        _fields_ = [(name, ctypes.c_ulonglong) for name in
                    ('ReadOperationCount', 'WriteOperationCount', 'OtherOperationCount',
                     'ReadTransferCount', 'WriteTransferCount', 'OtherTransferCount')]

    class ExtendedLimits(ctypes.Structure):
        _fields_ = [('BasicLimitInformation', BasicLimits), ('IoInfo', IoCounters),
                    ('ProcessMemoryLimit', size_t), ('JobMemoryLimit', size_t),
                    ('PeakProcessMemoryUsed', size_t), ('PeakJobMemoryUsed', size_t)]

    class Accounting(ctypes.Structure):
        _fields_ = [('TotalUserTime', ctypes.c_longlong), ('TotalKernelTime', ctypes.c_longlong),
                    ('ThisPeriodTotalUserTime', ctypes.c_longlong), ('ThisPeriodTotalKernelTime', ctypes.c_longlong),
                    ('TotalPageFaultCount', w.DWORD), ('TotalProcesses', w.DWORD),
                    ('ActiveProcesses', w.DWORD), ('TotalTerminatedProcesses', w.DWORD)]

    class Startup(ctypes.Structure):
        _fields_ = [('cb', w.DWORD), ('lpReserved', w.LPWSTR), ('lpDesktop', w.LPWSTR), ('lpTitle', w.LPWSTR),
                    ('dwX', w.DWORD), ('dwY', w.DWORD), ('dwXSize', w.DWORD), ('dwYSize', w.DWORD),
                    ('dwXCountChars', w.DWORD), ('dwYCountChars', w.DWORD), ('dwFillAttribute', w.DWORD),
                    ('dwFlags', w.DWORD), ('wShowWindow', w.WORD), ('cbReserved2', w.WORD),
                    ('lpReserved2', ctypes.POINTER(w.BYTE)), ('hStdInput', w.HANDLE),
                    ('hStdOutput', w.HANDLE), ('hStdError', w.HANDLE)]

    class StartupEx(ctypes.Structure):
        _fields_ = [('StartupInfo', Startup), ('lpAttributeList', ctypes.c_void_p)]

    class ProcessInfo(ctypes.Structure):
        _fields_ = [('hProcess', w.HANDLE), ('hThread', w.HANDLE), ('dwProcessId', w.DWORD), ('dwThreadId', w.DWORD)]

    kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, w.LPCWSTR]
    kernel.CreateJobObjectW.restype = w.HANDLE
    kernel.SetInformationJobObject.argtypes = [w.HANDLE, ctypes.c_int, ctypes.c_void_p, w.DWORD]
    kernel.SetInformationJobObject.restype = w.BOOL
    kernel.AssignProcessToJobObject.argtypes = [w.HANDLE, w.HANDLE]
    kernel.AssignProcessToJobObject.restype = w.BOOL
    kernel.TerminateJobObject.argtypes = [w.HANDLE, w.UINT]
    kernel.TerminateJobObject.restype = w.BOOL
    kernel.QueryInformationJobObject.argtypes = [w.HANDLE, ctypes.c_int, ctypes.c_void_p, w.DWORD, ctypes.c_void_p]
    kernel.QueryInformationJobObject.restype = w.BOOL
    kernel.InitializeProcThreadAttributeList.argtypes = [ctypes.c_void_p, w.DWORD, w.DWORD, ctypes.POINTER(size_t)]
    kernel.InitializeProcThreadAttributeList.restype = w.BOOL
    kernel.UpdateProcThreadAttribute.argtypes = [ctypes.c_void_p, w.DWORD, size_t, ctypes.c_void_p, size_t,
                                                ctypes.c_void_p, ctypes.c_void_p]
    kernel.UpdateProcThreadAttribute.restype = w.BOOL
    kernel.DeleteProcThreadAttributeList.argtypes = [ctypes.c_void_p]
    kernel.CreateProcessW.argtypes = [w.LPCWSTR, w.LPWSTR, ctypes.c_void_p, ctypes.c_void_p, w.BOOL,
                                     w.DWORD, ctypes.c_void_p, w.LPCWSTR, ctypes.POINTER(StartupEx),
                                     ctypes.POINTER(ProcessInfo)]
    kernel.CreateProcessW.restype = w.BOOL
    kernel.ResumeThread.argtypes = [w.HANDLE]
    kernel.ResumeThread.restype = w.DWORD
    kernel.WaitForSingleObject.argtypes = [w.HANDLE, w.DWORD]
    kernel.WaitForSingleObject.restype = w.DWORD
    kernel.GetExitCodeProcess.argtypes = [w.HANDLE, ctypes.POINTER(w.DWORD)]
    kernel.GetExitCodeProcess.restype = w.BOOL
    kernel.TerminateProcess.argtypes = [w.HANDLE, w.UINT]
    kernel.TerminateProcess.restype = w.BOOL
    kernel.CloseHandle.argtypes = [w.HANDLE]
    kernel.CloseHandle.restype = w.BOOL

    def checked(ok):
        if not ok:
            raise ctypes.WinError(ctypes.get_last_error())

    job = kernel.CreateJobObjectW(None, None)
    checked(job)
    process = ProcessInfo()
    attributes = None
    attributes_initialized = False
    assigned = False
    try:
        limits = ExtendedLimits()
        limits.BasicLimitInformation.LimitFlags = 0x00002000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        checked(kernel.SetInformationJobObject(job, 9, ctypes.byref(limits), ctypes.sizeof(limits)))
        required = size_t()
        kernel.InitializeProcThreadAttributeList(None, 1, 0, ctypes.byref(required))
        if not required.value:
            raise OSError('Cannot size the process handle allowlist.')
        attributes = ctypes.create_string_buffer(required.value)
        checked(kernel.InitializeProcThreadAttributeList(attributes, 1, 0, ctypes.byref(required)))
        attributes_initialized = True
        with log.open('wb') as output, open(os.devnull, 'rb') as null_input:
            handles = (w.HANDLE * 2)(msvcrt.get_osfhandle(null_input.fileno()), msvcrt.get_osfhandle(output.fileno()))
            previous = [os.get_handle_inheritable(handle) for handle in handles]
            try:
                for handle in handles:
                    os.set_handle_inheritable(handle, True)
                checked(kernel.UpdateProcThreadAttribute(attributes, 0, 0x00020002,
                                                          handles, ctypes.sizeof(handles), None, None))
                startup = StartupEx()
                startup.StartupInfo.cb = ctypes.sizeof(startup)
                startup.StartupInfo.dwFlags = 0x00000100  # STARTF_USESTDHANDLES
                startup.StartupInfo.hStdInput = handles[0]
                startup.StartupInfo.hStdOutput = handles[1]
                startup.StartupInfo.hStdError = handles[1]
                startup.lpAttributeList = ctypes.cast(attributes, ctypes.c_void_p)
                command = ctypes.create_unicode_buffer(subprocess.list2cmdline([str(arg) for arg in argv]))
                block = '\0'.join(f'{key}={value}' for key, value in sorted(environment.items(), key=lambda item: item[0].upper())) + '\0\0'
                environment_block = ctypes.create_unicode_buffer(block)
                flags = 0x00000004 | 0x00000400 | 0x00080000 | 0x08000000
                checked(kernel.CreateProcessW(str(argv[0]), command, None, None, True, flags,
                                               environment_block, str(cwd), ctypes.byref(startup), ctypes.byref(process)))
            finally:
                for handle, old in zip(handles, previous):
                    os.set_handle_inheritable(handle, old)
            # The initial thread is still suspended; no child process can have escaped assignment.
            checked(kernel.AssignProcessToJobObject(job, process.hProcess))
            assigned = True
            if kernel.ResumeThread(process.hThread) == 0xFFFFFFFF:
                raise ctypes.WinError(ctypes.get_last_error())
            wait = kernel.WaitForSingleObject(process.hProcess, math.ceil(timeout * 1000))
            if wait not in (0, 258):
                raise ctypes.WinError(ctypes.get_last_error())
            timed_out = wait == 258
            exit_code = w.DWORD()
            if not timed_out:
                checked(kernel.GetExitCodeProcess(process.hProcess, ctypes.byref(exit_code)))
            # Reap background children even after a successful primary process exit.
            checked(kernel.TerminateJobObject(job, 124 if timed_out else 0))
            deadline = time.monotonic() + 10
            while True:
                accounting = Accounting()
                checked(kernel.QueryInformationJobObject(job, 1, ctypes.byref(accounting), ctypes.sizeof(accounting), None))
                if accounting.ActiveProcesses == 0:
                    break
                if time.monotonic() >= deadline:
                    raise OSError('Owned Job Object still contains active processes after termination.')
                time.sleep(0.02)
            return {'exit_code': 124 if timed_out else exit_code.value, 'timed_out': timed_out,
                    'cleanup': 'owned Job Object has zero active processes'}
    finally:
        # A failed assignment leaves only a suspended owned primary process, never executing unowned work.
        if process.hProcess and not assigned:
            kernel.TerminateProcess(process.hProcess, 125)
            kernel.WaitForSingleObject(process.hProcess, 10000)
        if job:
            kernel.CloseHandle(job)
        if process.hThread:
            kernel.CloseHandle(process.hThread)
        if process.hProcess:
            kernel.CloseHandle(process.hProcess)
        if attributes_initialized:
            kernel.DeleteProcThreadAttributeList(attributes)
