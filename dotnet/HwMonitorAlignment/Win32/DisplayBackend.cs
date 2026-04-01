using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Devices.Display;
using HwMonitorAlignment.Models;

namespace HwMonitorAlignment.Win32;

/// <summary>
/// Encapsulates all Win32 monitor enumeration and display-settings logic.
/// Friendly names are resolved via the WinRT Windows.Devices.Display.DisplayMonitor API
/// (available on Windows 10 1809+). All other operations use Win32 P/Invoke directly.
/// </summary>
public static class DisplayBackend
{
    // ----------------------------------------------------------------
    // Enumeration
    // ----------------------------------------------------------------

    /// <summary>
    /// Enumerates all active monitors and returns a list of MonitorInfo objects.
    /// </summary>
    public static List<MonitorInfo> GetMonitors()
    {
        // Pre-fetch friendly names from WinRT; falls back gracefully if unavailable.
        var friendlyNames = GetFriendlyNamesViaWinRT();

        var monitors = new List<MonitorInfo>();
        uint iDevNum = 0;

        while (iDevNum < 64)
        {
            var dd = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(null, iDevNum, ref dd, 0))
                break;

            iDevNum++;

            if ((dd.StateFlags & NativeConstants.DISPLAY_DEVICE_ACTIVE) == 0)
                continue;

            var devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!NativeMethods.EnumDisplaySettings(dd.DeviceName, NativeConstants.ENUM_CURRENT_SETTINGS, ref devMode))
                continue;

            bool isPrimary = (dd.StateFlags & NativeConstants.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

            // Walk the monitor devices attached to this adapter to get the model ID.
            string friendlyName = dd.DeviceString; // fallback: adapter description
            uint iMonNum = 0;
            while (iMonNum < 16)
            {
                var ddMon = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!NativeMethods.EnumDisplayDevices(dd.DeviceName, iMonNum, ref ddMon, 1))
                    break;

                iMonNum++;

                if ((ddMon.StateFlags & NativeConstants.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                    continue;

                // DeviceID format: "MONITOR\DELA0BA\{guid}\0001" — second segment is the model ID.
                string modelId = ExtractModelId(ddMon.DeviceID);
                if (!string.IsNullOrEmpty(modelId) &&
                    friendlyNames.TryGetValue(modelId, out var fn) &&
                    !string.IsNullOrEmpty(fn))
                {
                    friendlyName = fn;
                }
                else if (!string.IsNullOrEmpty(ddMon.DeviceString))
                {
                    friendlyName = ddMon.DeviceString;
                }
                break;
            }

            monitors.Add(new MonitorInfo
            {
                DeviceName      = dd.DeviceName,
                AdapterName     = dd.DeviceString,
                FriendlyName    = friendlyName,
                X               = devMode.dmPositionX,
                Y               = devMode.dmPositionY,
                Width           = (int)devMode.dmPelsWidth,
                Height          = (int)devMode.dmPelsHeight,
                Orientation     = devMode.dmDisplayOrientation,
                IsPrimary       = isPrimary,
                OriginalDevMode = devMode,
                CurrentDevMode  = devMode,
            });
        }

        return monitors;
    }

    // ----------------------------------------------------------------
    // Friendly-name lookup via Windows.Devices.Display.DisplayMonitor
    // ----------------------------------------------------------------

    /// <summary>
    /// Returns a map of monitor model ID (e.g. "DELA0BA") → friendly display name
    /// (e.g. "Dell U2720Q") using the WinRT DisplayMonitor API.
    /// Returns an empty dictionary on any failure so callers can fall back gracefully.
    /// </summary>
    private static Dictionary<string, string> GetFriendlyNamesViaWinRT()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Run on a thread-pool thread to avoid STA/WinRT async deadlock.
            var winrtMonitors = Task.Run(async () =>
                await DisplayMonitor.FindAllAsync()).GetAwaiter().GetResult();

            foreach (var m in winrtMonitors)
            {
                if (string.IsNullOrEmpty(m.DisplayName)) continue;
                // DeviceId format: "\\?\DISPLAY#DELA0BA#5&2abc#..." — second segment is the model ID.
                string modelId = ExtractModelId(m.DeviceId);
                if (!string.IsNullOrEmpty(modelId))
                    result.TryAdd(modelId, m.DisplayName);
            }
        }
        catch { /* Silently fall back to adapter names */ }
        return result;
    }

    /// <summary>
    /// Extracts the monitor model identifier from a device path.
    /// Works for both "MONITOR\DELA0BA\..." and "\\?\DISPLAY#DELA0BA#..." formats.
    /// </summary>
    private static string ExtractModelId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return string.Empty;
        var parts = deviceId.Split(new[] { '\\', '#' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : string.Empty;
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

}
