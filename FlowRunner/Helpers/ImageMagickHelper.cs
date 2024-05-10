using System.Diagnostics;
using FileFlows.Plugin;
using FileFlows.Plugin.Helpers;
using FileFlows.Shared;

namespace FileFlows.FlowRunner.Helpers;

/// <summary>
/// Helper for ImageMagick
/// </summary>
internal class ImageMagickHelper
{
    private bool? _CanUseImageMagick = null;
    /// <summary>
    /// Semaphore used to check if imagemagick can be used
    /// </summary>
    private FairSemaphore _semaphore = new(1);

    /// <summary>
    /// The ImageMagick executable files
    /// </summary>
    private readonly string EXE_CONVERT, EXE_IDENTIFY;

    public ImageMagickHelper(NodeParameters args)
    {
        EXE_CONVERT = args.GetToolPath("convert")?.EmptyAsNull() ?? "convert";
        EXE_IDENTIFY = args.GetToolPath("identify")?.EmptyAsNull() ?? "identify";
    }
    
    /// <summary>
    /// Gets if ImageMagick can be used
    /// </summary>
    /// <returns></returns>
    public bool CanUseImageMagick()
    {
        _semaphore.WaitAsync().Wait();
        try
        {
            if (_CanUseImageMagick == null)
            {
                bool isConvertAvailable = IsCommandAvailable(EXE_CONVERT);
                bool isIdentifyAvailable = IsCommandAvailable(EXE_IDENTIFY);
                _CanUseImageMagick = isConvertAvailable && isIdentifyAvailable;
            }

            return _CanUseImageMagick == true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    

    /// <summary>
    /// Checks if the specified command is available in the system path.
    /// </summary>
    /// <param name="command">The name of the command to check.</param>
    /// <returns>True if the command is available, otherwise false.</returns>
    bool IsCommandAvailable(string command)
    {
        try
        {
            // Start a process to execute the specified command with the --version argument
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Start the process
            using Process? process = Process.Start(startInfo);
            // Wait for the process to exit
            process.WaitForExit();
            // Check if the process exited successfully (exit code 0)
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.WLog(ex.Message);
            // An exception occurred, command is not available
            return false;
        }
    }
    /// <summary>
    /// Gets the image dimensions
    /// </summary>
    /// <param name="imagePath">the path to the file</param>
    /// <returns>the image dimensions</returns>
    public Result<(int Width, int Height)> GetImageDimensions(string imagePath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = EXE_IDENTIFY, // ImageMagick's identify command
            ArgumentList = {"-format", "%w %h", imagePath},
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            return Result<(int, int)>.Fail("Failed to get image dimensions using ImageMagick");

        string[] dimensions = output.Trim().Split(' ');
        if (dimensions.Length != 2 || int.TryParse(dimensions[0], out var width) == false ||
            int.TryParse(dimensions[1], out var height) == false)
            return Result<(int, int)>.Fail("Invalid image dimensions retrieved from ImageMagick");

        return (width, height);
    }
    
    /// <summary>
    /// Converts an image from one format to another and applies optional resizing options.
    /// </summary>
    /// <param name="imagePath">The path to the input image file.</param>
    /// <param name="destination">The path to save the converted image.</param>
    /// <param name="options">Optional parameters for resizing the image.</param>
    /// <returns>A result indicating whether the conversion was successful.</returns>
    public Result<bool> ConvertImage(string imagePath, string destination, ImageOptions? options)
    {
        try
        {
            // Execute ImageMagick command for resizing
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = EXE_CONVERT, // ImageMagick's convert command
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(imagePath);
            if (options != null)
            {
                // Get image dimensions using ImageMagick
                var result = GetImageDimensions(imagePath);
                if (result.Failed(out string error))
                    return Result<bool>.Fail(error);
                
                (int width, int height) = result.Value;

                // Apply image resizing logic
                (int newWidth, int newHeight) = ImageHelper.CalculateNewDimensions(width, height, options);
                
                switch (options.Mode)
                {
                    case ResizeMode.Contain:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}>");
                        break;
                    case ResizeMode.Cover:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}^");
                        break;
                    case ResizeMode.Fill:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}!");
                        break;
                    case ResizeMode.Min:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}>");
                        break;
                    case ResizeMode.Max:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}<");
                        break;
                    case ResizeMode.Pad:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}");
                        startInfo.ArgumentList.Add("-background");
                        startInfo.ArgumentList.Add("#ffffff");
                        startInfo.ArgumentList.Add("-gravity");
                        startInfo.ArgumentList.Add("center");
                        startInfo.ArgumentList.Add("-extent");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}");
                        break;
                    default:
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{newWidth}x{newHeight}!");
                        break;
                }

                if (options.Quality != null)
                    AddQuality(startInfo, destination, options.Quality.Value);
            } 
            startInfo.ArgumentList.Add(destination);
            
            using Process? process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
                return Result<bool>.Fail("Failed to resize image using ImageMagick");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }


    /// <summary>
    /// Flips an image
    /// </summary>
    /// <param name="imagePath">The path to the input image.</param>
    /// <param name="destination">The file path where the resized image will be saved.</param>
    /// <param name="quality">The image quality, only used by some image types</param>
    /// <param name="vertically">true for vertically, otherwise false for horizontally</param>
    /// <returns>A result indicating whether the operation was successful or not.</returns>
    public Result<bool> FlipImage(string imagePath, string destination, int quality, bool vertically)
    {
        try
        {
            // Execute ImageMagick command for resizing
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = EXE_CONVERT, // ImageMagick's convert command
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(imagePath);
            startInfo.ArgumentList.Add(vertically ? "-flip" : "-flop"); // -flip for vertical, -flop for horizontal
            
            AddQuality(startInfo, destination, quality);

            startInfo.ArgumentList.Add(destination);
            
            using Process? process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
                return Result<bool>.Fail("Failed to flip image using ImageMagick");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Rotates an image
    /// </summary>
    /// <param name="imagePath">The path to the input image.</param>
    /// <param name="destination">The file path where the resized image will be saved.</param>
    /// <param name="degrees">the degrees of rotation</param>
    /// <param name="quality">The image quality, only used by some image types</param>
    /// <returns>A result indicating whether the operation was successful or not.</returns>
    public Result<bool> Rotate(string imagePath, string destination, int degrees, int quality)
    {
        try
        {
            // Execute ImageMagick command for resizing
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = EXE_CONVERT, // ImageMagick's convert command
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(imagePath);
            startInfo.ArgumentList.Add("-rotate");
            startInfo.ArgumentList.Add(degrees.ToString());
            
            AddQuality(startInfo, destination, quality);
            
            startInfo.ArgumentList.Add(destination);
            
            using Process? process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
                return Result<bool>.Fail("Failed to flip image using ImageMagick");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Trims black or white edges from an image
    /// </summary>
    /// <param name="imagePath">The path to the input image.</param>
    /// <param name="destination">The file path where the resized image will be saved.</param>
    /// <param name="fuzzPercent">the percent, 0 to 100 for the fuzziness trimming of the image</param>
    /// <param name="quality">The image quality, only used by some image types</param>
    /// <returns>True if the image was trimmed, or false if stayed the same size.</returns>
    public Result<bool> Trim(string imagePath, string destination, int fuzzPercent, int quality)
    {
        try
        {
            (int origWidth, int origHeight) = GetImageDimensions(imagePath).Value;
            // Execute ImageMagick command for resizing
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = EXE_CONVERT, // ImageMagick's convert command
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(imagePath);
            if (fuzzPercent > 0)
            {startInfo.ArgumentList.Add("-fuzz");
                startInfo.ArgumentList.Add(fuzzPercent + "%");
            }

            startInfo.ArgumentList.Add("-trim");
            
            AddQuality(startInfo, destination, quality);
            
            startInfo.ArgumentList.Add(destination);
            
            using Process? process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
                return Result<bool>.Fail("Failed to flip image using ImageMagick");
            
            (int newWidth, int newHeight) = GetImageDimensions(destination).Value;

            return newWidth != origWidth || newHeight != origHeight;
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Adds the quality to the start info if needed
    /// </summary>
    /// <param name="startInfo">the process start info</param>
    /// <param name="destination">the destination file</param>
    /// <param name="quality">the quality value</param>
    private void AddQuality(ProcessStartInfo startInfo, string destination, int quality)
    {
        // Add quality parameter if the image format is JPEG or WebP
        string fileExtension = Path.GetExtension(destination).ToLowerInvariant();
        if (fileExtension is ".jpg" or ".jpe" or "jpeg" or ".webp")
        {
            startInfo.ArgumentList.Add("-quality");
            startInfo.ArgumentList.Add(quality.ToString());
        }
    }

    /// <summary>
    /// Gets the date the image was taken
    /// </summary>
    /// <param name="imagePath">the path to the image</param>
    /// <returns>the datetime the image was taken, or a failure result if could not be obtained</returns>
    public string? GetDateTaken(string imagePath)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = EXE_IDENTIFY, // ImageMagick's identify command
                ArgumentList = {"-format", "%[EXIF:DateTimeOriginal]", imagePath},
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode != 0 ? null : output.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}