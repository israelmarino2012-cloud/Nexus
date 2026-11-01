/*
  Nexus visual theme for the native Windows build.
  Dark title bar, dark menu bar and popup menus, dark window background and
  a minimal start screen. Menu structure and options are NOT changed.
  Uses only dynamic lookups for newer Windows APIs, so it degrades safely
  (the window just stays with the standard look) on older systems.
*/
#ifndef NEXUS_THEME_H
#define NEXUS_THEME_H

#include <windows.h>

namespace NexusTheme
{
    static const COLORREF kBg     = RGB(15, 17, 21);
    static const COLORREF kRaised = RGB(30, 34, 43);
    static const COLORREF kBorder = RGB(39, 44, 56);
    static const COLORREF kText   = RGB(215, 219, 230);
    static const COLORREF kMuted  = RGB(123, 130, 150);

    /* Undocumented-but-stable menu bar messages used by dark mode implementations */
    #define NX_WM_UAHDRAWMENU      0x0091
    #define NX_WM_UAHDRAWMENUITEM  0x0092

    struct NxUahMenu { HMENU hmenu; HDC hdc; DWORD dwFlags; };
    /* Only the leading fields we read; the real structure is larger. */
    struct NxUahDrawMenuItem { DRAWITEMSTRUCT dis; NxUahMenu um; int iPosition; };

    inline HBRUSH BgBrush()     { static HBRUSH b = CreateSolidBrush(kBg);     return b; }
    inline HBRUSH RaisedBrush() { static HBRUSH b = CreateSolidBrush(kRaised); return b; }

    inline DWORD OsBuild()
    {
        typedef LONG (WINAPI *RtlGetVersionFn)(OSVERSIONINFOW *);
        static DWORD build = 0;
        if (!build)
        {
            build = 1; /* unknown */
            HMODULE nt = GetModuleHandleW(L"ntdll.dll");
            if (nt)
            {
                RtlGetVersionFn fn = (RtlGetVersionFn)GetProcAddress(nt, "RtlGetVersion");
                OSVERSIONINFOW vi;
                ZeroMemory(&vi, sizeof(vi));
                vi.dwOSVersionInfoSize = sizeof(vi);
                if (fn && fn(&vi) == 0)
                    build = vi.dwBuildNumber;
            }
        }
        return build;
    }

    /* Call once before the menu / window are created: dark popup menus. */
    inline void InitApp()
    {
        if (OsBuild() < 18362) /* Windows 10 1903+ */
            return;

        HMODULE ux = LoadLibraryW(L"uxtheme.dll");
        if (!ux)
            return;

        typedef int  (WINAPI *SetPreferredAppModeFn)(int);
        typedef void (WINAPI *FlushMenuThemesFn)(void);

        SetPreferredAppModeFn setMode = (SetPreferredAppModeFn)GetProcAddress(ux, MAKEINTRESOURCEA(135));
        FlushMenuThemesFn     flush   = (FlushMenuThemesFn)GetProcAddress(ux, MAKEINTRESOURCEA(136));

        if (setMode)
            setMode(2); /* ForceDark */
        if (flush)
            flush();
    }

    /* Call once after the main window is created: dark title bar. */
    inline void ApplyToWindow(HWND hwnd)
    {
        HMODULE dwm = LoadLibraryW(L"dwmapi.dll");
        if (!dwm)
            return;

        typedef HRESULT (WINAPI *DwmSetWindowAttributeFn)(HWND, DWORD, LPCVOID, DWORD);
        DwmSetWindowAttributeFn set = (DwmSetWindowAttributeFn)GetProcAddress(dwm, "DwmSetWindowAttribute");
        if (!set)
            return;

        BOOL on = TRUE;
        if (FAILED(set(hwnd, 20, &on, sizeof(on))))   /* DWMWA_USE_IMMERSIVE_DARK_MODE */
            set(hwnd, 19, &on, sizeof(on));           /* same, pre-20H1 value */

        if (OsBuild() >= 22000) /* Windows 11: match title bar to the theme */
        {
            COLORREF caption = kBg, border = kBorder, text = kText;
            set(hwnd, 35, &caption, sizeof(caption)); /* DWMWA_CAPTION_COLOR */
            set(hwnd, 34, &border,  sizeof(border));  /* DWMWA_BORDER_COLOR  */
            set(hwnd, 36, &text,    sizeof(text));    /* DWMWA_TEXT_COLOR    */
        }
    }

    inline bool MenuBarRect(HWND hwnd, RECT *out)
    {
        MENUBARINFO mbi;
        ZeroMemory(&mbi, sizeof(mbi));
        mbi.cbSize = sizeof(mbi);
        if (!GetMenuBarInfo(hwnd, OBJID_MENU, 0, &mbi))
            return false;

        RECT win;
        GetWindowRect(hwnd, &win);
        *out = mbi.rcBar;
        OffsetRect(out, -win.left, -win.top);
        return true;
    }

    /* Removes the thin light line Windows draws under a custom-drawn menu bar. */
    inline void PaintMenuBottomLine(HWND hwnd)
    {
        RECT client, win;
        GetClientRect(hwnd, &client);
        MapWindowPoints(hwnd, NULL, (POINT *)&client, 2);
        GetWindowRect(hwnd, &win);
        OffsetRect(&client, -win.left, -win.top);

        RECT line = client;
        line.bottom = line.top;
        line.top    = line.top - 1;

        HDC hdc = GetWindowDC(hwnd);
        if (hdc)
        {
            FillRect(hdc, &line, BgBrush());
            ReleaseDC(hwnd, hdc);
        }
    }

    inline void DrawMenuBarItem(NxUahDrawMenuItem *p)
    {
        wchar_t text[256];
        text[0] = 0;

        MENUITEMINFOW mii;
        ZeroMemory(&mii, sizeof(mii));
        mii.cbSize     = sizeof(mii);
        mii.fMask      = MIIM_STRING;
        mii.dwTypeData = text;
        mii.cch        = 255;
        GetMenuItemInfoW(p->um.hmenu, (UINT)p->iPosition, TRUE, &mii);

        HDC  hdc = p->um.hdc;
        RECT rc  = p->dis.rcItem;
        UINT st  = p->dis.itemState;

        bool hot      = (st & (ODS_HOTLIGHT | ODS_SELECTED)) != 0;
        bool inactive = (st & (ODS_INACTIVE | ODS_DISABLED | ODS_GRAYED)) != 0;

        FillRect(hdc, &rc, hot ? RaisedBrush() : BgBrush());

        static HFONT font = NULL;
        if (!font)
        {
            NONCLIENTMETRICSW ncm;
            ZeroMemory(&ncm, sizeof(ncm));
            ncm.cbSize = sizeof(ncm);
            if (SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(ncm), &ncm, 0))
                font = CreateFontIndirectW(&ncm.lfMenuFont);
        }
        HGDIOBJ old = font ? SelectObject(hdc, font) : NULL;

        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, inactive ? kMuted : (hot ? RGB(255, 255, 255) : kText));

        UINT flags = DT_CENTER | DT_SINGLELINE | DT_VCENTER;
        if (st & ODS_NOACCEL)
            flags |= DT_HIDEPREFIX;
        DrawTextW(hdc, text, -1, &rc, flags);

        if (old)
            SelectObject(hdc, old);
    }

    /* Returns true if the message was handled (result in *out). */
    inline bool HandleMessage(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp, LRESULT *out)
    {
        switch (msg)
        {
        case NX_WM_UAHDRAWMENU:
            {
                NxUahMenu *m = (NxUahMenu *)lp;
                RECT rc;
                if (m && MenuBarRect(hwnd, &rc))
                {
                    FillRect(m->hdc, &rc, BgBrush());
                    *out = 0;
                    return true;
                }
                return false;
            }
        case NX_WM_UAHDRAWMENUITEM:
            if (lp)
            {
                DrawMenuBarItem((NxUahDrawMenuItem *)lp);
                *out = 0;
                return true;
            }
            return false;
        case WM_NCPAINT:
        case WM_NCACTIVATE:
            *out = DefWindowProc(hwnd, msg, wp, lp);
            if (GetMenu(hwnd))
                PaintMenuBottomLine(hwnd);
            return true;
        }
        return false;
    }

    /* Minimal start screen, shown while no ROM is loaded. */
    inline bool PaintSplash(HWND hwnd, HINSTANCE inst, int iconResId)
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        if (!hdc)
            return false;

        RECT rc;
        GetClientRect(hwnd, &rc);
        FillRect(hdc, &rc, BgBrush());

        int w = rc.right - rc.left;
        int h = rc.bottom - rc.top;

        int icon = (h >= 300) ? 96 : ((h >= 200) ? 64 : 40);
        int titlePx = icon * 7 / 20;
        int subPx   = icon / 7 < 12 ? 12 : icon / 7;

        int total = icon + icon / 4 + titlePx * 2 + subPx * 2;
        int top   = (h - total) / 2;
        if (top < 4)
            top = 4;

        HICON ic = (HICON)LoadImage(inst, MAKEINTRESOURCE(iconResId), IMAGE_ICON, icon, icon, LR_DEFAULTCOLOR);
        if (ic)
        {
            DrawIconEx(hdc, (w - icon) / 2, top, ic, icon, icon, 0, NULL, DI_NORMAL);
            DestroyIcon(ic);
        }

        SetBkMode(hdc, TRANSPARENT);

        HFONT titleFont = CreateFontW(-titlePx, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                                      OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                                      DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        HFONT subFont   = CreateFontW(-subPx, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                                      OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                                      DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        int y = top + icon + icon / 4;

        HGDIOBJ old = SelectObject(hdc, titleFont);
        SetTextColor(hdc, kText);
        RECT r1 = { 0, y, w, y + titlePx * 2 };
        DrawTextW(hdc, L"N E X U S", -1, &r1, DT_CENTER | DT_SINGLELINE | DT_VCENTER);

        SelectObject(hdc, subFont);
        SetTextColor(hdc, kMuted);
        int y2 = y + titlePx * 2;
        RECT r2 = { 0, y2, w, y2 + subPx * 2 };
        DrawTextW(hdc, L"Open a ROM from the File menu", -1, &r2, DT_CENTER | DT_SINGLELINE | DT_VCENTER);

        SelectObject(hdc, old);
        DeleteObject(titleFont);
        DeleteObject(subFont);

        EndPaint(hwnd, &ps);
        return true;
    }
}

#endif /* NEXUS_THEME_H */
