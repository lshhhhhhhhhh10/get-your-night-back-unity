// Offline documentation runner. It compiles and executes the actual CGDD exporter.
// Unity editor services are no-ops here; this is not a Unity compilation or playtest.
using System;
using System.IO;
namespace UnityEngine {
 public static class Application {
  public static string dataPath;
  public static string productName = "Get Your Night Back";
  public static string companyName = "Coursework";
  public static bool isBatchMode = true;
  public static void OpenURL(string url) { }
 }
 public static class Debug {
  public static void Log(object value) { Console.WriteLine(value); }
  public static void LogWarning(object value) { Console.WriteLine(value); }
  public static void LogException(Exception value) { Console.Error.WriteLine(value); }
 }
}
namespace UnityEditor {
 [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem : Attribute {
  public MenuItem(string value, bool validate=false, int priority=0) { }
 }
 public static class AssetDatabase {
  public static void Refresh() { }
  public static bool IsValidFolder(string path) { return false; }
  public static bool DeleteAsset(string path) { throw new NotSupportedException(); }
 }
 public static class EditorApplication { public static void Exit(int code) { Environment.Exit(code); } }
 public static class EditorUtility {
  public static bool DisplayDialog(string title,string message,string ok,string cancel="") { return false; }
 }
}
public static class OfflineExport {
 public static void Main(string[] args) {
  UnityEngine.Application.dataPath = Path.Combine(Path.GetFullPath(args[0]), "Assets");
  TraceDocsExporter.ExportCli();
 }
}
