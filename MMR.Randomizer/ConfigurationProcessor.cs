using MMR.Randomizer.Models;
using MMR.Randomizer.Models.Settings;
using MMR.Randomizer.Patch;
using MMR.Randomizer.Utils;
using System;

namespace MMR.Randomizer
{
    public enum ProcessState
    {
        Success,
        Exception,
        Cancelled,
        InvalidROM,
    }

    public class ProcessResult
    {
        public ProcessState State { get; }

        public string ErrorMessage { get; }

        public ProcessResult(ProcessState state, string errorMsg = null)
        {
            this.State = state;
            this.ErrorMessage = errorMsg;
        }
    }

    public static class ConfigurationProcessor
    {
        public static ProcessResult Process(Configuration configuration, int seed, IProgressReporter progressReporter, Callbacks callbacks = null)
        {
            var randomizer = new Randomizer(configuration.GameplaySettings, seed);
            RandomizedResult randomized = null;
            if (string.IsNullOrWhiteSpace(configuration.OutputSettings.InputPatchFilename))
            {
                try
                {
                    randomized = randomizer.Randomize(progressReporter);
                }
                catch (RandomizationException ex)
                {
                    string nl = Environment.NewLine;
                    return new ProcessResult(ProcessState.Exception,
                        $"Error randomizing logic: {ex.Message}{nl}{nl}Please try a different seed");
                }
                catch (Exception ex)
                {
                    return new ProcessResult(ProcessState.Exception, ex.Message);
                }

                if (configuration.OutputSettings.GenerateSpoilerLog
                    && configuration.GameplaySettings.LogicMode != LogicMode.Vanilla)
                {
                    SpoilerUtils.CreateSpoilerLog(randomized, configuration.GameplaySettings, configuration.OutputSettings);
                }
            }

            if (configuration.OutputSettings.GenerateROM || configuration.OutputSettings.OutputVC || configuration.OutputSettings.GeneratePatch)
            {
                if (!RomUtils.ValidateROM(configuration.OutputSettings.InputROMFilename))
                {
                    return new ProcessResult(ProcessState.InvalidROM, "Cannot verify input ROM is Majora's Mask (U).");
                }

                var builder = new Builder(randomized, configuration.CosmeticSettings);

                try
                {
                    var result = builder.MakeROM(configuration.OutputSettings, progressReporter, callbacks);

                    if (result)
                        return new ProcessResult(ProcessState.Success);
                    else
                        return new ProcessResult(ProcessState.Cancelled);
                }
                catch (PatchMagicException)
                {
                    return new ProcessResult(ProcessState.Exception, $"Error applying patch: Not a valid patch file");
                }
                catch (PatchFormatException ex)
                {
                    return new ProcessResult(ProcessState.Exception, $"Error applying patch: {ex.Message}");
                }
                catch (Exception ex)
                {
                    string nl = Environment.NewLine;
                    return new ProcessResult(ProcessState.Exception,
                        $"Error building ROM: {ex.Message}{nl}{nl}Please contact the development team and provide them more information");
                }
            }

            //settings.InputPatchFilename = null;

            return new ProcessResult(ProcessState.Success);
            //return "Generation complete!";
        }
    }

    public interface IProgressReporter
    {
        void ReportProgress(int percentProgress, string message);
    }
}
