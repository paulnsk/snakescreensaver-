using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SnakeScreensaver;

internal static class Settings
{
    public enum RenderMode { Balls = 0, Ribbon = 1 }
    public enum ExitMode { EscapeOnly = 0, MouseMove = 1 }
    public enum Renderer { Wpf = 0, Skia = 1, WpfTurbo = 2 }

    public static RenderMode Mode = RenderMode.Balls;
    public static bool Rainbow = true;
    public static ExitMode Exit = ExitMode.EscapeOnly;
    public static int IdleMinutes = 5;
    public static bool RunAtStartup = false;
    public static Renderer Render = Renderer.Wpf;
    public static bool DropShadow = true;
    public static bool LivingSnake = true;
    public static bool MagicPoop = true;

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnakeScreensaver");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    static Settings()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            Mode = RenderMode.Balls;
            Rainbow = true;
            Exit = ExitMode.EscapeOnly;
            IdleMinutes = 5;
            Render = Renderer.Wpf;
            DropShadow = true;
            LivingSnake = true;
            MagicPoop = true;
            RunAtStartup = ReadStartupFlag();
            if (!File.Exists(FilePath)) return;
            var txt = File.ReadAllText(FilePath);
            if (MatchInt(txt, "Mode", out var mi)) Mode = (RenderMode)mi;
            if (MatchBool(txt, "Rainbow", out var rb)) Rainbow = rb;
            if (MatchInt(txt, "Exit", out var ex)) Exit = (ExitMode)ex;
            if (MatchInt(txt, "IdleMinutes", out var im) && im >= 0 && im <= 240) IdleMinutes = im;
            if (MatchInt(txt, "Render", out var rr)) Render = (Renderer)rr;
            if (MatchBool(txt, "DropShadow", out var ds)) DropShadow = ds;
            if (MatchBool(txt, "LivingSnake", out var ls)) LivingSnake = ls;
            if (MatchBool(txt, "MagicPoop", out var mp)) MagicPoop = mp;
            if (MatchBool(txt, "RunAtStartup", out var rs)) RunAtStartup = rs;
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            var json =
                "{\n" +
                $"  \"Mode\": {(int)Mode},\n" +
                $"  \"Rainbow\": {(Rainbow ? "true" : "false")},\n" +
                $"  \"Exit\": {(int)Exit},\n" +
                $"  \"IdleMinutes\": {IdleMinutes},\n" +
                $"  \"Render\": {(int)Render},\n" +
                $"  \"DropShadow\": {(DropShadow ? "true" : "false")},\n" +
                $"  \"LivingSnake\": {(LivingSnake ? "true" : "false")},\n" +
                $"  \"MagicPoop\": {(MagicPoop ? "true" : "false")},\n" +
                $"  \"RunAtStartup\": {(RunAtStartup ? "true" : "false")}\n" +
                "}";
            File.WriteAllText(FilePath, json);
            ApplyStartupFlag(RunAtStartup);
        }
        catch { }
    }

    private static bool MatchInt(string txt, string key, out int v)
    {
        v = 0;
        var m = Regex.Match(txt, "\"" + key + "\"\\s*:\\s*(\\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out v);
    }

    private static bool MatchBool(string txt, string key, out bool v)
    {
        v = false;
        var m = Regex.Match(txt, "\"" + key + "\"\\s*:\\s*(true|false)");
        if (!m.Success) return false;
        v = m.Groups[1].Value == "true";
        return true;
    }

    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SnakeScreensaver";

    public static bool ReadStartupFlag()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return k?.GetValue(RunValueName) != null;
        }
        catch { return false; }
    }

    public static void ApplyStartupFlag(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (on)
            {
                var exe = System.Windows.Forms.Application.ExecutablePath;
                k.SetValue(RunValueName, $"\"{exe}\"");
            }
            else k.DeleteValue(RunValueName, false);
        }
        catch { }
    }
}
