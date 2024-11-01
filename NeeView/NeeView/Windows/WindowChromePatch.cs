﻿//#define LOCAL_DEBUG

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using NeeView.Interop;


namespace NeeView.Windows
{
    /// <summary>
    /// WindowChrome が適用されたウィンドウ挙動の問題を補正する
    /// </summary>
    public class WindowChromePatch
    {
        private readonly Window _window;
        private readonly WindowChrome _windowChrome;
        private readonly Thickness _resizeBorderThickness;

        public WindowChromePatch(Window window, WindowChrome windowChrome)
        {
            if (window is null) throw new ArgumentNullException(nameof(window));
            if (windowChrome is null) throw new ArgumentNullException(nameof(windowChrome));

            Debug.Assert(WindowChrome.GetWindowChrome(window) is null, "Already chromed");

            _window = window;
            _windowChrome = windowChrome;
            _resizeBorderThickness = windowChrome.ResizeBorderThickness;

            WindowChrome.SetWindowChrome(_window, _windowChrome);
            _window.StateChanged += Window_StateChanged;

            if (!AddHook())
            {
                _window.SourceInitialized += (s, e) => AddHook();
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            // NOTE: タブレットモードでのフルスクリーン時に画面端のカーソル座標が取得できなくなる現象の対策として ResizeBorderThickness を 0 にする。
            _windowChrome.ResizeBorderThickness = _window.WindowState == WindowState.Maximized ? new Thickness(0) : _resizeBorderThickness;
        }

        private bool AddHook()
        {
            var hWnd = new WindowInteropHelper(_window).Handle;
            if (hWnd != IntPtr.Zero)
            {
                HwndSource.FromHwnd(hWnd).AddHook(new HwndSourceHook(WndProc));
                return true;
            }
            else
            {
                return false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((WindowMessages)msg)
            {
                case WindowMessages.WM_NCCALCSIZE:
                    // Adjust client area
                    // https://mntone.hateblo.jp/entry/2020/08/02/111309
                    if (wParam != IntPtr.Zero)
                    {
                        var result = this.CalcNonClientSize(hwnd, lParam, ref handled);
                        if (handled) return result;
                    }
                    break;

                case WindowMessages.WM_NCHITTEST:
                    // This prevents a crash in WindowChromeWorker._HandleNCHitTest
                    // https://developercommunity.visualstudio.com/content/problem/167357/overflow-exception-in-windowchrome.html?childToView=1209945#comment-1209945 
                    try
                    {
                        var x = lParam.ToInt32();
                        ////DebugInfo.Current?.SetMessage($"WM_NCHITTEST.LPARAM: {x:#,0}");
                        ////Debug.WriteLine($"{x:#,0}");
                    }
                    catch (OverflowException)
                    {
                        handled = true;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 最大化時のクライアントエリアの調整
        /// </summary>
        /// <remarks>
        /// The MIT License<br/>
        /// Copyright 2020 mntone<br/>
        /// https://mntone.hateblo.jp/entry/2020/08/02/111309
        /// </remarks>
        /// <returns></returns>
        private IntPtr CalcNonClientSize(IntPtr hWnd, IntPtr lParam, ref bool handled)
        {
            if (!NativeMethods.IsZoomed(hWnd)) return IntPtr.Zero;

            // タブレットモードでは処理しない
            if (WindowParameters.IsTabletMode) return IntPtr.Zero;

            var nonClientCalcSize = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
            Trace("RCSize.LPPos.Flags {0}", nonClientCalcSize.lppos.flags);

            // NOTE: タスクバーが横に配置されているときに幅によっては SWP_NOSIZE が立って補正できないため、SWP_NOSIZE のときも補正するようにした 
            //if (nonClientCalcSize.lppos.flags.HasFlag(SetWindowPosFlags.SWP_NOSIZE)) return IntPtr.Zero;

            // NOTE: 最小化から復元するタイミングではウィンドウがタスクバーのモニター側に存在することがあるため、座標から対象モニターを取得する
            Trace($"RCSize.rgrc[0] {nonClientCalcSize.rgrc[0]}");
            var sourceArea = nonClientCalcSize.rgrc[0];
            var windowCenter = new POINT((sourceArea.right + sourceArea.left) / 2, (sourceArea.top + sourceArea.bottom) / 2);
            Trace($"RCSize.rgrc[0].Center: {windowCenter}");

            var hMonitor = NativeMethods.MonitorFromPoint(windowCenter, (int)MonitorDefaultTo.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return IntPtr.Zero;

            (bool isSuccess, RECT workArea) = GetMonitorWorkArea(hMonitor, (_window.WindowStyle == WindowStyle.None));
            if (!isSuccess) return IntPtr.Zero;

            // 安全のため、補正サイズが元のサイズを超える場合は補正しない
            if (workArea.left < sourceArea.left || sourceArea.right < workArea.right || workArea.top < sourceArea.top || sourceArea.bottom < workArea.bottom)
            {
                Trace($"Skip: RCSize is over the original range.");
                return IntPtr.Zero;
            }

            nonClientCalcSize.rgrc[0] = workArea;
            nonClientCalcSize.rgrc[1] = workArea;
            nonClientCalcSize.rgrc[2] = workArea;
            Marshal.StructureToPtr(nonClientCalcSize, lParam, true);

            handled = true;
            return (IntPtr)(WindowValidRects.WVR_ALIGNTOP | WindowValidRects.WVR_ALIGNLEFT | WindowValidRects.WVR_VALIDRECTS);
        }

        /// <summary>
        /// モニターのワークエリア取得
        /// </summary>
        /// <param name="hMonitor">モニターハンドル</param>
        /// <param name="isFullScreen">フルスクリーンフラグ。 false のときはタスクバーを考慮しない</param>
        /// <returns></returns>
        private static (bool, RECT) GetMonitorWorkArea(IntPtr hMonitor, bool isFullScreen)
        {
            var monitorInfo = new MONITORINFO() { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return (false, new RECT());
            Trace($"MonitorInfo: Monitor={monitorInfo.rcMonitor}, Work={monitorInfo.rcWork}, Flags={monitorInfo.dwFlags}");

            RECT workArea;
            if (isFullScreen)
            {
                // フルスクリーン時はモニターサイズにする。
                workArea = monitorInfo.rcMonitor;
            }
            else
            {
                // 通常時はモニターのワークサイズを指定。タスクバーが自動非表示の場合のマージンも加味する。
                workArea = monitorInfo.rcWork;
                AppBar.ApplyAppbarSpace(hMonitor, monitorInfo.rcMonitor, ref workArea);

                // もしタブレットモードでモニターサイズと一致している(非表示タスクバー情報の取得に失敗している)場合の補正
                // モニターサイズで SetWindowPos() を呼んでも変化がない現象の対策
                if (WindowParameters.IsTabletMode && workArea.Equals(monitorInfo.rcMonitor))
                {
                    Trace($"Cannot get auto hide AppBar");
                    workArea.bottom--;
                }
            }
            Trace($"Calc.RCSize {workArea}");

            return (true, workArea);
        }

        /// <summary>
        /// 最大化ウィンドウサイズ補正 (タブレットモード専用)
        /// </summary>
        /// <param name="window">ウィンドウ</param>
        /// <param name="isFullScreen">フルスクリーン？</param>
        public static void ResetMaximizedWindowSize(Window window, bool isFullScreen)
        {
            // タブレットモードのみの処理
            if (!WindowParameters.IsTabletMode) return;

            var hWnd = new WindowInteropHelper(window).Handle;
            if (hWnd == IntPtr.Zero) return;

            //var windowInfo = new WINDOWINFO() { cbSize = (int)Marshal.SizeOf(typeof(WINDOWINFO)) };
            //if (!NativeMethods.GetWindowInfo(hWnd, ref windowInfo)) return;
            //var windowRect = windowInfo.rcWindow;
            //Trace($"Window.Source: {windowInfo.rcWindow}");

            var hMonitor = NativeMethods.MonitorFromWindow(hWnd, (int)MonitorDefaultTo.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return;

            (bool isSuccess, RECT rect) = GetMonitorWorkArea(hMonitor, isFullScreen);
            if (isSuccess)
            {
                Trace($"Window.Reset: {rect}");
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, rect.left, rect.top, rect.Width, rect.Height, NativeMethods.SWP_NOZORDER);
            }
        }


        [Conditional("LOCAL_DEBUG")]
        private static void Trace(string message)
        {
            Debug.WriteLine($"{nameof(WindowChromePatch)}: {message}");
        }

        [Conditional("LOCAL_DEBUG")]
        private static void Trace(string format, params object[] args)
        {
            Debug.WriteLine($"{nameof(WindowChromePatch)}: {string.Format(CultureInfo.InvariantCulture, format, args)}");
        }
    }
}
