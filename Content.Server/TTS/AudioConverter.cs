using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Content.Server.TTS
{
    public static class AudioConverter
    {
        public static byte[] ConvertToOgg(byte[] inputAudioBytes)
        {
            var inputPath = Path.Combine(Path.GetTempPath(), $"tts_in_{Guid.NewGuid()}.opus");
            var outputPath = Path.Combine(Path.GetTempPath(), $"tts_out_{Guid.NewGuid()}.ogg");

            File.WriteAllBytes(inputPath, inputAudioBytes);

            try
            {
                var ffmpeg = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" " + "-filter:a loudnorm=I=-16:TP=-1.5:LRA=11 " + $" -c:a libvorbis \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(ffmpeg)!;
                var stderr = process.StandardError.ReadToEnd();
                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Logger.GetSawmill("tts").Error($"FFmpeg ConvertToOgg failed with exit code {process.ExitCode}: {stderr}");
                    throw new Exception($"FFmpeg failed: {stderr}");
                }

                if (!File.Exists(outputPath))
                    throw new Exception($"FFmpeg failed to produce an output file. Exit code: {process.ExitCode}\nStdout: {stdout}\nStderr: {stderr}");

                return File.ReadAllBytes(outputPath);
            }
            finally
            {
                try { File.Delete(inputPath); } catch { }
                try { File.Delete(outputPath); } catch { }
            }
        }

        public static async Task<byte[]> ApplyRadioEffect(byte[] inputAudioBytes)
        {
            var inputPath = Path.Combine(Path.GetTempPath(), $"tts_in_{Guid.NewGuid()}.ogg");
            var outputPath = Path.Combine(Path.GetTempPath(), $"tts_out_{Guid.NewGuid()}.ogg");

            File.WriteAllBytes(inputPath, inputAudioBytes);

            try
            {
                // We use a simpler filter string without internal quotes for better platform compatibility when UseShellExecute is false.
                var filter = "[0:a]" +
                             "aresample=8000," +
                             "highpass=f=300:poles=2," +
                             "lowpass=f=3400:poles=2," +
                             "equalizer=f=1500:width_type=o:width=1.5:g=8," +
                             "acompressor=threshold=-12dB:ratio=8:attack=0.5:release=30:makeup=4," +
                             "acrusher=level_in=2:level_out=1:bits=12:mode=lin:aa=1," +
                             "aresample=44100," +
                             "loudnorm=I=-16:TP=-1.5:LRA=11" +
                             "[radio];" +
                             "aevalsrc=random(0)*0.04:sample_rate=44100[noise];" +
                             "[radio][noise]amix=inputs=2:weights=1 0.1:duration=shortest[out]";

                var ffmpeg = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" -filter_complex \"{filter}\" -map \"[out]\" -c:a libvorbis \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(ffmpeg)!;
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (!File.Exists(outputPath))
                    throw new Exception($"FFmpeg failed to produce an output file for radio effect. Exit code: {process.ExitCode}\nStdout: {stdout}\nStderr: {stderr}");

                return File.ReadAllBytes(outputPath);
            }
            finally
            {
                try { File.Delete(inputPath); } catch { }
                try { File.Delete(outputPath); } catch { }
            }
        }
    }
}
