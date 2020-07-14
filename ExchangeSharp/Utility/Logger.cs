/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#region Imports

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using System.Diagnostics;

#endregion Imports

namespace ExchangeSharp
{
    /// <summary>
    /// Log levels
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Trace / Diagnostic
        /// </summary>
        Trace,

        /// <summary>
        /// Trace / Diagnostic
        /// </summary>
        Diagnostic = Trace,

        /// <summary>
        /// Debug
        /// </summary>
        Debug,

        /// <summary>
        /// Information / Info
        /// </summary>
        Information,

        /// <summary>
        /// Information / Info
        /// </summary>
        Info = Information,

        /// <summary>
        /// Warning / Warn
        /// </summary>
        Warning,

        /// <summary>
        /// Warning / Warn
        /// </summary>
        Warn = Warning,

        /// <summary>
        /// Error / Exception
        /// </summary>
        Error,

        /// <summary>
        /// Error / Exception
        /// </summary>
        Exception = Error,

        /// <summary>
        /// Critical / Fatal
        /// </summary>
        Critical,

        /// <summary>
        /// Critical / Fatal
        /// </summary>
        Fatal = Critical,

        /// <summary>
        /// Off / None
        /// </summary>
        Off,

        /// <summary>
        /// Off / None
        /// </summary>
        None = Off
    }

    /// <summary>
    /// ExchangeSharp logger. Will never throw exceptions.
    /// Currently the ExchangeSharp logger uses NLog internally, so make sure it is setup in your app.config file or nlog.config file.
    /// </summary>
    public static class Logger
    {
        private static readonly NLog.Logger logger;

        static Logger()
        {
            try
            {
                LogFactory factory = null;
                if (File.Exists(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath))
                {
                    factory = LogManager.LoadConfiguration(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath);
                }
				{
					if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config")))
                    {
                        factory = LogManager.LoadConfiguration(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
                    }
                    else
                    {
                        Console.WriteLine("Failed to Find file nlog.congfig in :"+Directory.GetCurrentDirectory());
                        System.IO.StringReader sr = new System.IO.StringReader(ExchangeSharpResources.NLog_config);
                        System.Xml.XmlReader xr = System.Xml.XmlReader.Create(sr);
                        LogManager.Configuration = new XmlLoggingConfiguration(xr, Directory.GetCurrentDirectory());
                        factory = LogManager.LogFactory;
                    }
                }
                logger = factory.GetCurrentClassLogger();
            }
            catch (Exception ex)
            {
                // log to console as no other logger is available
                Console.WriteLine("Failed to initialize logger: {0}", ex);
            }
        }

        /// <summary>
        /// Map IPBan log level to NLog log level
        /// </summary>
        /// <param name="logLevel">IPBan log level</param>
        /// <returns>NLog log level</returns>
        public static NLog.LogLevel GetNLogLevel(ExchangeSharp.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case ExchangeSharp.LogLevel.Critical: return NLog.LogLevel.Fatal;
                case ExchangeSharp.LogLevel.Debug: return NLog.LogLevel.Debug;
                case ExchangeSharp.LogLevel.Error: return NLog.LogLevel.Error;
                case ExchangeSharp.LogLevel.Information: return NLog.LogLevel.Info;
                case ExchangeSharp.LogLevel.Trace: return NLog.LogLevel.Trace;
                case ExchangeSharp.LogLevel.Warning: return NLog.LogLevel.Warn;
                default: return NLog.LogLevel.Off;
            }
        }

        /*
        /// <summary>
        /// Map Microsoft log level to NLog log level
        /// </summary>
        /// <param name="logLevel">Microsoft log level</param>
        /// <returns>NLog log level</returns>
        public static NLog.LogLevel GetNLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Critical: return NLog.LogLevel.Fatal;
                case Microsoft.Extensions.Logging.LogLevel.Debug: return NLog.LogLevel.Debug;
                case Microsoft.Extensions.Logging.LogLevel.Error: return NLog.LogLevel.Error;
                case Microsoft.Extensions.Logging.LogLevel.Information: return NLog.LogLevel.Info;
                case Microsoft.Extensions.Logging.LogLevel.Trace: return NLog.LogLevel.Trace;
                case Microsoft.Extensions.Logging.LogLevel.Warning: return NLog.LogLevel.Warn;
                default: return NLog.LogLevel.Off;
            }
        }
        */

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="ex">Error</param>
        public static void Error(Exception ex)
        {
            Write(ExchangeSharp.LogLevel.Error, "Exception: " + ex.ToString());
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="text">Text</param>
        /// <param name="args">Format arguments</param>
        public static void Error(string text, params object[] args)
        {
//             string log = string.Format(text, args);
//             Console.WriteLine(DateTime.Now + " [ERROR] " + log);
            Write(ExchangeSharp.LogLevel.Error, text, args);
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="ex">Error</param>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Error(Exception ex, string text, params object[] args)
        {
            Write(ExchangeSharp.LogLevel.Error, string.Format(text, args) + ": " + ex.ToString());
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Warn(string text, params object[] args)
        {
            Write(ExchangeSharp.LogLevel.Warning, text, args);
        }


        /// <summary>
        /// Log an info message
        /// </summary>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Info(string text, params object[] args)
        {
            Write(ExchangeSharp.LogLevel.Info, text, args);
        }

        /// <summary>
        /// Log a debug message
        /// </summary>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Debug(string text, params object[] args)
        {
            Write(ExchangeSharp.LogLevel.Debug, text, args);
            //Console.WriteLine(string.Format(DateTime.Now + " " + text, args));
        }
        /// <summary>
        /// Log a Trace message
        /// </summary>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Trace(string text, params object[] args)
        {
            Write(ExchangeSharp.LogLevel.Trace, text, args);
            //Console.WriteLine(string.Format(DateTime.Now + " " + text, args));
        }
        /// <summary>
        /// Write to the log
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="text">Text with format</param>
        /// <param name="args">Format args</param>
        public static void Write(ExchangeSharp.LogLevel level, string text, params object[] args)
        {
            try
            {
                if (args != null && args.Length != 0)
                {
                    text = string.Format(text, args);
                }
                if(!text.StartsWith("{") && !text.StartsWith("\""))//如果不是json格式那么双引号
                    text = "\"" + text + "\"";

                logger?.Log(GetNLogLevel(level), text);
            }
            catch
            {
                // oh well...
            }
        }
    }
}

[LayoutRenderer("stacktrace")]
public class CustomStackTraseLayoutRenderer : StackTraceLayoutRenderer
{
    [DefaultValue(0)]
    public int SkipFrames { get; set; } // configurable

    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        int startingFrame = logEvent.UserStackFrameNumber + TopFrames - 1;
        if (startingFrame >= logEvent.StackTrace.FrameCount)
            startingFrame = logEvent.StackTrace.FrameCount - 1;

        bool first = true;
        int endingFrame = logEvent.UserStackFrameNumber + SkipFrames;
        for (int i = startingFrame; i >= endingFrame; --i)
        {
            StackFrame f = logEvent.StackTrace.GetFrame(i);

            switch (Format)
            {
                case StackTraceFormat.Raw:
                    builder.Append(f.ToString()+"adsfasdfa");
                    break;
                case StackTraceFormat.Flat:
                    if (!first)
                        builder.Append(this.Separator);
                    var type = f.GetMethod().DeclaringType;
                    builder.Append(type == null ? "<no type>" : type.Name);
                    builder.Append(".");
                    builder.Append(f.GetMethod().Name);
                    first = false;
                    break;
                case StackTraceFormat.DetailedFlat:
                    if (!first)
                        builder.Append(this.Separator);
                    builder.Append("[" + f.GetMethod() + "]");
                    first = false;
                    break;
            }
        }
    }
}