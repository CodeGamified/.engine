// CodeGamified.Logging — Shared logging framework
// MIT License
//
// Intercepts all Unity Debug.Log/LogWarning/LogError calls via
// Application.logMessageReceived and writes them to a timestamped
// log file on disk. Also captures stack traces for errors.
//
// Usage:
//   LogFile.Initialize();                // call once at boot
//   LogFile.Initialize("MyGame");        // custom prefix
//   // ... all Debug.Log calls are now captured automatically ...
//   string path = LogFile.CurrentPath;   // read the file path
//   LogFile.Flush();                     // force flush to disk
//   LogFile.Shutdown();                  // close on app quit

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CodeGamified.Logging
{
    /// <summary>
    /// Singleton log file writer. Hooks into Unity's log callback and writes
    /// every message to a plain-text file in the project's Logs/ folder.
    ///
    /// File format: [HH:mm:ss.fff] [TYPE] message
    /// Errors include stack traces.
    /// File is flushed every N lines or on error for crash resilience.
    /// </summary>
    public static class LogFile
    {
        private static StreamWriter _writer;
        private static int _linesSinceFlush;
        private static bool _initialized;

        private const int FlushInterval = 20; // flush every N lines
        private const string LogDir = "Logs";

        /// <summary>Full path to the current log file.</summary>
        public static string CurrentPath { get; private set; }

        /// <summary>
        /// Initialize the log file. Call once during bootstrap.
        /// Safe to call multiple times — subsequent calls are no-ops.
        /// </summary>
        /// <param name="prefix">Filename prefix (e.g. "SeaRauber").</param>
        public static void Initialize(string prefix = "codegamified")
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Logs/ folder next to Assets/ (project root), not inside Assets
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string logFolder = Path.Combine(projectRoot, LogDir);
                Directory.CreateDirectory(logFolder);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"{prefix}_{timestamp}.log";
                CurrentPath = Path.Combine(logFolder, fileName);

                _writer = new StreamWriter(CurrentPath, false, Encoding.UTF8)
                {
                    AutoFlush = false
                };

                // Header
                _writer.WriteLine($"═══════════════════════════════════════════════════════");
                _writer.WriteLine($" {prefix} Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($" Unity {Application.unityVersion} | {Application.platform}");
                _writer.WriteLine($" {SystemInfo.operatingSystem} | {SystemInfo.graphicsDeviceName}");
                _writer.WriteLine($"═══════════════════════════════════════════════════════");
                _writer.WriteLine();
                _writer.Flush();

                // Hook Unity log callback
                Application.logMessageReceived += OnLogMessage;
                Application.quitting += Shutdown;

                Debug.Log($"[LogFile] Writing to: {CurrentPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogFile] Failed to initialize: {ex.Message}");
                _initialized = false;
            }
        }

        /// <summary>Force flush buffered lines to disk.</summary>
        public static void Flush()
        {
            _writer?.Flush();
            _linesSinceFlush = 0;
        }

        /// <summary>Close the log file. Called automatically on Application.quitting.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            Application.logMessageReceived -= OnLogMessage;

            try
            {
                _writer?.WriteLine();
                _writer?.WriteLine($"═══ Log closed {DateTime.Now:HH:mm:ss} ═══");
                _writer?.Flush();
                _writer?.Close();
                _writer?.Dispose();
            }
            catch { /* swallow — we're shutting down */ }

            _writer = null;
        }

        /// <summary>Write a custom line directly (bypasses Debug.Log).</summary>
        public static void Write(string message)
        {
            if (_writer == null) return;
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer.WriteLine($"[{time}] {message}");
            CheckFlush();
        }

        // ═══════════════════════════════════════════════════════
        // UNITY LOG CALLBACK
        // ═══════════════════════════════════════════════════════

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_writer == null) return;

            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string tag = type switch
            {
                LogType.Error     => "ERR",
                LogType.Assert    => "AST",
                LogType.Warning   => "WRN",
                LogType.Exception => "EXC",
                _                 => "LOG",
            };

            _writer.WriteLine($"[{time}] [{tag}] {message}");

            // Include stack trace for errors/exceptions
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !string.IsNullOrEmpty(stackTrace))
            {
                // Indent stack trace lines
                foreach (var line in stackTrace.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        _writer.WriteLine($"         {line.TrimEnd()}");
                }
            }

            // Flush immediately on errors, otherwise periodically
            if (type == LogType.Error || type == LogType.Exception)
                Flush();
            else
                CheckFlush();
        }

        private static void CheckFlush()
        {
            if (++_linesSinceFlush >= FlushInterval)
                Flush();
        }
    }
}
