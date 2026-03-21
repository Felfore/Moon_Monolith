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
            var inputPath = Path.Combine("/tmp", $"tts_in_{Guid.NewGuid()}.opus");
            var outputPath = Path.Combine("/tmp", $"tts_out_{Guid.NewGuid()}.ogg");

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
                process.WaitForExit();

                if (!File.Exists(outputPath))
                    throw new FileNotFoundException("FFmpeg did not produce an output file.");

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
            var inputPath = Path.Combine("/tmp", $"tts_in_{Guid.NewGuid()}.ogg");
            var outputPath = Path.Combine("/tmp", $"tts_out_{Guid.NewGuid()}.ogg");

            File.WriteAllBytes(inputPath, inputAudioBytes);

            try
            {
                var ffmpeg = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" " +
                                "-filter_complex " +
                                "\"[0:a]" +
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
                                "[radio][noise]amix=inputs=2:weights=1 0.1:duration=shortest[out]\" " +
                                "-map \"[out]\" " +
                                $"-c:a libvorbis \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(ffmpeg)!;
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                await Task.WhenAll(stderrTask, stdoutTask);
                await process.WaitForExitAsync();

                if (!File.Exists(outputPath))
                    throw new FileNotFoundException("FFmpeg did not produce an output file.");

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
