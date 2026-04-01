using System.Runtime.InteropServices;

namespace HwMonitorAlignment.Win32;

// ============================================================
// Constants
// ============================================================
public static class NativeConstants
{
    public const uint DISPLAY_DEVICE_ACTIVE          = 0x00000001;
    public const uint DISPLAY_DEVICE_PRIMARY_DEVICE  = 0x00000004;
    public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    public const int  ENUM_CURRENT_SETTINGS          = -1;
    public const int  ENUM_REGISTRY_SETTINGS         = -2;
    public const uint DM_POSITION                    = 0x00000020;
    public const uint DM_PELSWIDTH                   = 0x00080000;
    public const uint DM_PELSHEIGHT                  = 0x00100000;
    public const uint CDS_UPDATEREGISTRY             = 0x00000001;
    public const uint CDS_NORESET                    = 0x10000000;
    public const uint CDS_GLOBAL                     = 0x00000008;
    public const int  DISP_CHANGE_SUCCESSFUL         = 0;
    public const uint QDC_ONLY_ACTIVE_PATHS          = 0x00000002;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    public const int  SM_XVIRTUALSCREEN              = 76;
    public const int  SM_YVIRTUALSCREEN              = 77;
    public const int  SM_CXVIRTUALSCREEN             = 78;
    public const int  SM_CYVIRTUALSCREEN             = 79;
}

// ============================================================
// Structs
// ============================================================

/// <summary>
/// DEVMODE structure with unsafe fixed-size buffers.
/// Pack=1 matches the Windows layout exactly.
/// dmPositionX/Y occupy the union slot normally used by dmPosition (POINTL).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct DEVMODE
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmDeviceName;
    public ushort dmSpecVersion;
    public ushort dmDriverVersion;
    public ushort dmSize;
    public ushort dmDriverExtra;
    public uint   dmFields;
    // Union: dmPosition (POINTL) / dmPosition2 / ...
    public int    dmPositionX;
    public int    dmPositionY;
    public uint   dmDisplayOrientation;
    public uint   dmDisplayFixedOutput;
    public short  dmColor;
    public short  dmDuplex;
    public short  dmYResolution;
    public short  dmTTOption;
    public short  dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmFormName;
    public ushort dmLogPixels;
    public uint   dmBitsPerPel;
    public uint   dmPelsWidth;
    public uint   dmPelsHeight;
    public uint   dmDisplayFlags;
    public uint   dmDisplayFrequency;
    public uint   dmICMMethod;
    public uint   dmICMIntent;
    public uint   dmMediaType;
    public uint   dmDitherType;
    public uint   dmReserved1;
    public uint   dmReserved2;
    public uint   dmPanningWidth;
    public uint   dmPanningHeight;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct DISPLAY_DEVICE
{
    public uint   cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;
    public uint   StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

// DisplayConfig structs for friendly name enumeration
[StructLayout(LayoutKind.Sequential)]
public struct LUID
{
    public uint  LowPart;
    public int   HighPart;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public LUID  adapterId;
    public uint  id;
    public uint  modeInfoIdx;
    public uint  statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public LUID   adapterId;
    public uint   id;
    public uint   modeInfoIdx;
    public uint   outputTechnology;
    public uint   rotation;
    public uint   scaling;
    public uint   refreshRate_Numerator;
    public uint   refreshRate_Denominator;
    public uint   scanLineOrdering;
    [MarshalAs(UnmanagedType.Bool)]
    public bool   targetAvailable;
    public uint   statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_MODE_INFO
{
    public uint        infoType;
    public uint        id;
    public LUID        adapterId;
    // 64-byte union; we only need the raw bytes
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[]      modeInfoData;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
{
    public uint value;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public uint type;
    public uint size;
    public LUID adapterId;
    public uint id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
    public uint outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}

// ============================================================
// P/Invoke declarations
// ============================================================
public static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplaySettings(
        string lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    // Overload for commit call (null DEVMODE)
    [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "ChangeDisplaySettingsExW")]
    public static extern int ChangeDisplaySettingsExCommit(
        IntPtr lpszDeviceName,
        IntPtr lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);
}
