using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HwMonitorAlignment.Models;

namespace HwMonitorAlignment.Win32;

/// <summary>
/// Encapsulates all Win32 monitor enumeration and display-settings logic.
/// </summary>
public static class DisplayBackend
{
    // ----------------------------------------------------------------
    // Enumeration
    // ----------------------------------------------------------------

    /// <summary>
    /// Enumerates all active monitors and returns a list of MonitorInfo objects.
    /// Friendly names are fetched via QueryDisplayConfig; on failure the
    /// DeviceString from DISPLAY_DEVICE is used as fallback.
    /// </summary>
    public static List<MonitorInfo> GetMonitors()
    {
        // Step 1: build a map from monitor device path -> friendly name
        var friendlyNames = TryGetFriendlyNames();

        var monitors = new List<MonitorInfo>();

        uint iDevNum = 0;
        while (iDevNum < 64)
        {
            var dd = new DISPLAY_DEVICE();
            dd.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>();

            if (!NativeMethods.EnumDisplayDevices(null, iDevNum, ref dd, 0))
                break;

            iDevNum++;

            // Only active adapters
            if ((dd.StateFlags & NativeConstants.DISPLAY_DEVICE_ACTIVE) == 0)
                continue;

            // Get current settings for this adapter
            var devMode = new DEVMODE();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
            if (!NativeMethods.EnumDisplaySettings(dd.DeviceName, NativeConstants.ENUM_CURRENT_SETTINGS, ref devMode))
                continue;

            bool isPrimary = (dd.StateFlags & NativeConstants.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

            // Try to get a friendly name by enumerating monitor devices attached to this adapter
            string friendlyName = dd.DeviceString; // fallback to adapter name
            string monitorDeviceId = "";

            uint iMonNum = 0;
            while (iMonNum < 16)
            {
                var ddMon = new DISPLAY_DEVICE();
                ddMon.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>();
                if (!NativeMethods.EnumDisplayDevices(dd.DeviceName, iMonNum, ref ddMon, 1))
                    break;

                iMonNum++;

                // DISPLAY_DEVICE_ACTIVE for monitors = ATTACHED_TO_DESKTOP (0x1)
                if ((ddMon.StateFlags & NativeConstants.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                    continue;

                monitorDeviceId = ddMon.DeviceID;

                // Look up in friendly names map
                if (!string.IsNullOrEmpty(monitorDeviceId) &&
                    friendlyNames.TryGetValue(monitorDeviceId, out var fn) &&
                    !string.IsNullOrEmpty(fn))
                {
                    friendlyName = fn;
                }
                else if (!string.IsNullOrEmpty(ddMon.DeviceString))
                {
                    friendlyName = ddMon.DeviceString;
                }
                break; // only need the first active monitor on this adapter
            }

            var monitor = new MonitorInfo
            {
                DeviceName   = dd.DeviceName,
                AdapterName  = dd.DeviceString,
                FriendlyName = friendlyName,
                X            = devMode.dmPositionX,
                Y            = devMode.dmPositionY,
                Width        = (int)devMode.dmPelsWidth,
                Height       = (int)devMode.dmPelsHeight,
                Orientation  = devMode.dmDisplayOrientation,
                IsPrimary    = isPrimary,
                OriginalDevMode = devMode,
                CurrentDevMode  = devMode,
            };

            monitors.Add(monitor);
        }

        return monitors;
    }

    // ----------------------------------------------------------------
    // Apply / Revert
    // ----------------------------------------------------------------

    /// <summary>
    /// Applies all monitor positions to the OS using ChangeDisplaySettingsEx.
    /// First stages each monitor with CDS_UPDATEREGISTRY | CDS_NORESET,
    /// then commits with a null call.
    /// Returns true if all calls returned DISP_CHANGE_SUCCESSFUL.
    /// </summary>
    public static bool ApplyMonitorPositions(IEnumerable<MonitorInfo> monitors)
    {
        bool allOk = true;
        uint flags = NativeConstants.CDS_UPDATEREGISTRY | NativeConstants.CDS_NORESET;

        foreach (var m in monitors)
        {
            var devMode = m.CurrentDevMode;
            devMode.dmFields = NativeConstants.DM_POSITION;
            devMode.dmPositionX = m.X;
            devMode.dmPositionY = m.Y;

            int result = NativeMethods.ChangeDisplaySettingsEx(
                m.DeviceName, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);

            if (result != NativeConstants.DISP_CHANGE_SUCCESSFUL)
                allOk = false;
        }

        // Commit all staged changes
        int commitResult = NativeMethods.ChangeDisplaySettingsExCommit(
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);

        return allOk && commitResult == NativeConstants.DISP_CHANGE_SUCCESSFUL;
    }

    // ----------------------------------------------------------------
    // Virtual screen helpers
    // ----------------------------------------------------------------

    public static (int x, int y, int width, int height) GetVirtualScreenBounds()
    {
        int x      = NativeMethods.GetSystemMetrics(NativeConstants.SM_XVIRTUALSCREEN);
        int y      = NativeMethods.GetSystemMetrics(NativeConstants.SM_YVIRTUALSCREEN);
        int width  = NativeMethods.GetSystemMetrics(NativeConstants.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeConstants.SM_CYVIRTUALSCREEN);
        return (x, y, width, height);
    }

    // ----------------------------------------------------------------
    // Friendly-name helpers via QueryDisplayConfig
    // ----------------------------------------------------------------

    /// <summary>
    /// Returns a dictionary mapping monitor device path -> friendly name.
    /// Returns an empty dictionary on any failure so callers can fall back gracefully.
    /// </summary>
    private static Dictionary<string, string> TryGetFriendlyNames()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            int ret = NativeMethods.GetDisplayConfigBufferSizes(
                NativeConstants.QDC_ONLY_ACTIVE_PATHS,
                out uint pathCount,
                out uint modeCount);

            if (ret != 0 || pathCount == 0)
                return result;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            ret = NativeMethods.QueryDisplayConfig(
                NativeConstants.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount, paths,
                ref modeCount, modes,
                IntPtr.Zero);

            if (ret != 0)
                return result;

            foreach (var path in paths)
            {
                var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                targetName.header.type       = NativeConstants.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetName.header.size       = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                targetName.header.adapterId  = path.targetInfo.adapterId;
                targetName.header.id         = path.targetInfo.id;

                int infoRet = NativeMethods.DisplayConfigGetDeviceInfo(ref targetName);
                if (infoRet == 0 && !string.IsNullOrEmpty(targetName.monitorDevicePath))
                {
                    result[targetName.monitorDevicePath] = targetName.monitorFriendlyDeviceName ?? "";
                }
            }
        }
        catch
        {
            // Silently ignore; callers use fallback names
        }
        return result;
    }
}
