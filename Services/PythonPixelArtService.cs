using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PixelArtGenerator.Models;

namespace PixelArtGenerator.Services
{
    public class PythonPixelArtService : IPythonPixelArtService
    {
        private readonly string _pythonPath;
        private readonly string _scriptsPath;
        private readonly string _tempDirectory;
        private readonly string _logPath;
        private static readonly object _lockObject = new object();

        public PythonPixelArtService(string pythonPath, string scriptsPath)
        {
            _pythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
            _scriptsPath = scriptsPath ?? throw new ArgumentNullException(nameof(scriptsPath));
            _tempDirectory = Path.Combine(Path.GetTempPath(), "PixelArtGenerator");
            _logPath = Path.Combine(_tempDirectory, "processing.log");
            
            // 确保临时目录存在
            Directory.CreateDirectory(_tempDirectory);
        }

        public async Task<Bitmap> ProcessImageAsync(Bitmap source, PixelArtOptions options, IProgress<int> progress = null)
        {
            var result = await ProcessImageWithResultAsync(source, options, progress);
            return result.ProcessedImage;
        }

        public async Task<ProcessingResult> ProcessImageWithResultAsync(Bitmap source, PixelArtOptions options, IProgress<int> progress = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var startTime = DateTime.Now;
            
            try
            {
                LogMessage($"开始处理图片: {options.PixelSize}px, {options.ColorCount} colors");
                
                // 创建临时文件
                var tempFiles = CreateTempFiles();
                
                try
                {
                    // 保存源图片
                    SaveBitmapToFile(source, tempFiles.InputPath);
                    LogMessage($"源图片已保存到: {tempFiles.InputPath}");
                    
                    // 构建命令行参数
                    var arguments = BuildCommandArguments(options, tempFiles);
                    LogMessage($"执行命令: {_pythonPath} \"{Path.Combine(_scriptsPath, "pixelate.py")}\" {arguments}");
                    
                    // 执行Python脚本
                    var result = await ExecutePythonScriptAsync(
                        Path.Combine(_scriptsPath, "pixelate.py"), 
                        arguments, 
                        tempFiles, 
                        progress);
                    
                    if (result.IsSuccess && File.Exists(tempFiles.OutputPath))
                    {
                        LogMessage($"处理成功，输出文件: {tempFiles.OutputPath}");
                        
                        // 加载处理后的图片
                        var processedImage = LoadBitmapFromFile(tempFiles.OutputPath);
                        
                        var processingTime = DateTime.Now - startTime;
                        LogMessage($"处理完成，耗时: {processingTime.TotalSeconds:F2}秒");
                        
                        return ProcessingResult.Success(
                            processedImage, 
                            processingTime);
                    }
                    else
                    {
                        LogMessage($"处理失败: {result.ErrorMessage}");
                        return ProcessingResult.Failure(result.ErrorMessage);
                    }
                }
                finally
                {
                    // 清理临时文件
                    CleanupTempFiles(tempFiles);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理异常: {ex}");
                return ProcessingResult.Failure($"处理失败: {ex.Message}");
            }
        }

        public async Task<Bitmap[]> BatchProcessAsync(Bitmap[] sources, PixelArtOptions options, IProgress<int> progress = null)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("源图片数组不能为空");

            var results = new Bitmap[sources.Length];
            var totalProgress = 0;
            
            for (int i = 0; i < sources.Length; i++)
            {
                var itemProgress = new Progress<int>(value =>
                {
                    var overallProgress = (i * 100 + value) / sources.Length;
                    progress?.Report(overallProgress);
                });
                
                results[i] = await ProcessImageAsync(sources[i], options, itemProgress);
            }
            
            return results;
        }

        public async Task<string[]> GetAvailablePalettesAsync()
        {
            try
            {
                var arguments = "--list-palettes";
                var result = await ExecutePythonScriptAsync(
                    Path.Combine(_scriptsPath, "palettes.py"), 
                    arguments, 
                    null, 
                    null);
                
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    var palettes = JsonSerializer.Deserialize<Dictionary<string, string>>(result.Output);
                    return palettes.Keys.ToArray();
                }
                
                return new[] { "default" };
            }
            catch
            {
                return new[] { "default" };
            }
        }

        public async Task<string[]> GetAvailableAlgorithmsAsync()
        {
            // 算法列表在C#代码中定义，保持同步
            return new[]
            {
                "basic",
                "average", 
                "median",
                "adaptive",
                "vector",
                "smooth"
            };
        }

        public async Task<System.Drawing.Color[]> GetPaletteColorsAsync(string paletteName, int colorCount = 256)
        {
            try
            {
                var arguments = $"--get-palette-colors {paletteName} {colorCount}";
                var result = await ExecutePythonScriptAsync(
                    Path.Combine(_scriptsPath, "palettes.py"), 
                    arguments, 
                    null, 
                    null);
                
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    var colors = JsonSerializer.Deserialize<List<List<int>>>(result.Output);
                    return colors.Select(c => System.Drawing.Color.FromArgb(c[0], c[1], c[2])).ToArray();
                }
                
                return new[] { System.Drawing.Color.Black, System.Drawing.Color.White };
            }
            catch
            {
                return new[] { System.Drawing.Color.Black, System.Drawing.Color.White };
            }
        }

        public async Task<bool> CheckPythonEnvironmentAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> InstallDependenciesAsync(IProgress<string> progress = null)
        {
            try
            {
                var requirementsPath = Path.Combine(_scriptsPath, "requirements.txt");
                
                if (!File.Exists(requirementsPath))
                {
                    progress?.Report("requirements.txt 文件未找到");
                    return false;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"-m pip install -r \"{requirementsPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        progress?.Report(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        progress?.Report($"ERROR: {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                progress?.Report($"安装依赖失败: {ex.Message}");
                return false;
            }
        }

        #region 私有方法
        private TempFiles CreateTempFiles()
        {
            var guid = Guid.NewGuid().ToString("N");
            return new TempFiles
            {
                InputPath = Path.Combine(_tempDirectory, $"{guid}_input.png"),
                OutputPath = Path.Combine(_tempDirectory, $"{guid}_output.png"),
                ProgressPath = Path.Combine(_tempDirectory, $"{guid}_progress.json")
            };
        }

        private string BuildCommandArguments(PixelArtOptions options, TempFiles tempFiles)
        {
            var sb = new StringBuilder();
            sb.Append($"\"{tempFiles.InputPath}\" ");
            sb.Append($"--output \"{tempFiles.OutputPath}\" ");
            sb.Append($"--pixel-size {options.PixelSize} ");
            sb.Append($"--color-count {options.ColorCount} ");
            sb.Append($"--palette \"{options.Palette}\" ");
            sb.Append($"--algorithm \"{options.Algorithm}\" ");
            
            if (options.EnableDithering)
                sb.Append("--dithering ");
            
            sb.Append($"--edge-smoothing {options.EdgeSmoothing:F2} ");
            sb.Append($"--contrast {options.Contrast:F2} ");
            sb.Append($"--brightness {options.Brightness:F2} ");
            sb.Append($"--saturation {options.Saturation:F2} ");
            sb.Append($"--dither-strength {options.DitherStrength:F2} ");
            
            if (options.EnableCartoonEffect)
                sb.Append("--cartoon-effect ");
            
            sb.Append($"--progress-file \"{tempFiles.ProgressPath}\" ");
            
            return sb.ToString();
        }

        private async Task<(bool IsSuccess, string Output, string ErrorMessage)> ExecutePythonScriptAsync(
            string scriptPath, string arguments, TempFiles tempFiles, IProgress<int> progress)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _scriptsPath
                }
            };

            try
            {
                LogMessage($"启动进程: {_pythonPath} \"{scriptPath}\" {arguments}");
                process.Start();
                
                // 监控进度（如果提供了进度文件）
                var progressTask = MonitorProgressAsync(tempFiles?.ProgressPath, progress);
                
                // 读取输出
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // 等待进程完成
                await process.WaitForExitAsync();
                
                if (progressTask != null)
                    await progressTask;
                
                var output = await outputTask;
                var error = await errorTask;
                
                LogMessage($"进程退出码: {process.ExitCode}");
                if (!string.IsNullOrEmpty(output))
                    LogMessage($"标准输出: {output}");
                if (!string.IsNullOrEmpty(error))
                    LogMessage($"错误输出: {error}");
                
                if (process.ExitCode == 0)
                {
                    return (true, output.Trim(), null);
                }
                else
                {
                    return (false, null, error.Trim());
                }
            }
            catch (Exception ex)
            {
                LogMessage($"执行脚本异常: {ex}");
                return (false, null, ex.Message);
            }
        }

        private async Task MonitorProgressAsync(string progressFilePath, IProgress<int> progress)
        {
            if (string.IsNullOrEmpty(progressFilePath) || progress == null)
                return;

            while (true)
            {
                try
                {
                    if (File.Exists(progressFilePath))
                    {
                        var json = await File.ReadAllTextAsync(progressFilePath);
                        var progressData = JsonSerializer.Deserialize<ProgressData>(json);
                        progress.Report(progressData.Progress);
                        
                        if (progressData.Progress >= 100)
                            break;
                    }
                }
                catch
                {
                    // 忽略进度读取错误
                }
                
                await Task.Delay(100);
            }
        }

        private void SaveBitmapToFile(Bitmap bitmap, string filePath)
        {
            lock (_lockObject)
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }
        }

        private Bitmap LoadBitmapFromFile(string filePath)
        {
            lock (_lockObject)
            {
                return new Bitmap(filePath);
            }
        }

        private void CleanupTempFiles(TempFiles tempFiles)
        {
            if (tempFiles == null)
                return;

            try
            {
                foreach (var file in new[] { tempFiles.InputPath, tempFiles.OutputPath, tempFiles.ProgressPath })
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
            }
            catch
            {
                // 忽略清理错误
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, logEntry);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        private class TempFiles
        {
            public string InputPath { get; set; }
            public string OutputPath { get; set; }
            public string ProgressPath { get; set; }
        }

        private class ProgressData
        {
            public int Progress { get; set; }
            public string Message { get; set; }
            public double Timestamp { get; set; }
        }
        #endregion
    }
}