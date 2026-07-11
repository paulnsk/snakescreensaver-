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

    public static RenderMode Mode = RenderMode.Balls;
    public static bool Rainbow = true;
    public static ExitMode Exit = ExitMode.EscapeOnly;
    public static int IdleMinutes = 5;
    public static bool RunAtStartup = false;

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
            RunAtStartup = ReadStartupFlag();
            if (!File.Exists(FilePath)) return;
            var txt = File.ReadAllText(FilePath);
            if (MatchInt(txt, "Mode", out var mi)) Mode = (RenderMode)mi;
            if (MatchBool(txt, "Rainbow", out var rb)) Rainbow = rb;
            if (MatchInt(txt, "Exit", out var ex)) Exit = (ExitMode)ex;
            if (MatchInt(txt, "IdleMinutes", out var im) && im >= 0 && im <= 240) IdleMinutes = im;
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
