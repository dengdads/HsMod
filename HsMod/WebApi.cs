﻿using BepInEx.Configuration;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HsMod
{
    public class WebApi
    {

        public static async Task<string> RunShellCommandAsync(string command)
        {
            var processInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Platform-specific settings for command execution
            if ((Environment.OSVersion.Platform == PlatformID.MacOSX) || (Environment.OSVersion.Platform == PlatformID.Unix))
            {
                processInfo.FileName = "/bin/sh";
                processInfo.Arguments = $"-c \"{command}\"";
            }
            else
            {
                processInfo.FileName = "cmd.exe";
                processInfo.Arguments = "/C chcp 65001 & " + command;
            }

            using (var process = new Process { StartInfo = processInfo })
            {
                var outputBuilder = new StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                // Set up asynchronous reading of output and error streams
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true); // Mark as complete when output ends
                    else
                        outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true); // Mark as complete when error output ends
                    else
                        outputBuilder.AppendLine(e.Data);
                };

                // Start the process and begin reading output and error
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Create a task to wait for the process to exit
                var processTask = Task.Run(() =>
                {
                    process.WaitForExit();
                    tcs.TrySetResult(true);
                });

                // Wait for the process to complete or timeout after 5 seconds
                var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completedTask == processTask)
                {
                    // Process completed within timeout
                    return outputBuilder.ToString();
                }
                else
                {
                    // Timeout occurred
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill(); // Ensure the process is terminated on timeout
                        }
                        catch
                        {
                            // Ignore any exceptions if the process is already terminated
                        }
                    }
                    return string.Empty; // Return empty string on timeout
                }
            }
        }



        public static int RunPluginConfigAsync(string key, string value, out string res)
        {
            res = string.Empty;

            if (!string.IsNullOrEmpty(key) && (key.Length > 5))
            {
                key = key.Substring(0, key.Length - 5); // remove .name
                if (key.Equals("isWebshellEnable"))
                {
                    res = "not allow.";
                    return 403;
                }
                var configKeyProp = typeof(PluginConfig).GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (configKeyProp == null)
                {
                    res = "key not found.";
                    return 500;

                }
                var configEntry = (ConfigEntryBase)configKeyProp.GetValue(null);
                var converter = TomlTypeConverter.GetConverter(configEntry.SettingType);
                if (converter != null)
                {
                    configEntry.SetSerializedValue(value);
                    res = configEntry.GetSerializedValue();
                    return 200;
                }
            }
            return 400;
        }


    }
}