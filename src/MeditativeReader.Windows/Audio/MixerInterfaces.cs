using NAudio.Wave;
using System.Threading.Tasks;

namespace MeditativeReader.Audio
{
    /// <summary>
    /// The three main output buses.
    /// </summary>
    public enum AudioChannel
    {
        Music,   // Frequencies processed through Flavor
        Rhythm,
        Voice
    }

    /// <summary>
    /// Playback mode determines the blend between ambient and rhythmic content.
    /// </summary>
    public enum PlaybackMode
    {
        Ambient,   // Music dominant, rhythm subtle or absent
        Hybrid,    // Balanced blend
        Rhythmic   // Rhythm dominant, music supportive
    }

    /// <summary>
    /// Controls how dominant the soundscape is in the mix.
    /// </summary>
    public enum Strength
    {
        Subtle,      // Background level
        Noticeable,  // Present but not dominant
        Dominant     // Foreground level
    }

    /// <summary>
    /// A single channel strip with volume, pan, and EQ.
    /// </summary>
    public interface IChannelStrip
    {
        AudioChannel Channel { get; }
        float VolumeDb { get; set; }           // Current volume in dB
        float TargetVolumeDb { get; set; }     // Where we are fading to
        float Pan { get; set; }                // -1.0 (L) to 1.0 (R)
        ISampleProvider Source { get; set; }

        /// <summary>
        /// Called every audio frame to interpolate volume toward target.
        /// </summary>
        void ProcessVolumeRamp(int samplesProcessed);
    }

    /// <summary>
    /// Handles all smooth transitions — no hard cuts unless forced.
    /// </summary>
    public interface ITransitionEngine
    {
        /// <summary>
        /// Fade a channel to target dB over duration.
        /// </summary>
        void FadeChannel(AudioChannel channel, float targetDb, float durationMs);

        /// <summary>
        /// Smoothly transition BPM from current to target.
        /// </summary>
        void GlideBpm(double targetBpm, float durationMs);

        /// <summary>
        /// Smoothly transition a specific oscillator frequency.
        /// </summary>
        void GlideFrequency(int oscillatorIndex, double targetFreq, float durationMs);

        /// <summary>
        /// Crossfade between playback modes (Ambient → Hybrid → Rhythmic).
        /// This adjusts the relative volumes of Music and Rhythm buses.
        /// </summary>
        void CrossfadeMode(PlaybackMode targetMode, float durationMs);

        /// <summary>
        /// Adjust strength with smooth volume transition across all affected channels.
        /// </summary>
        void TransitionStrength(Strength targetStrength, float durationMs);

        /// <summary>
        /// Master roll-off — fade entire mix to silence.
        /// </summary>
        Task RollOffAsync(float durationMs);

        /// <summary>
        /// Process all active transitions. Called from audio thread.
        /// </summary>
        void Update(int samplesElapsed);
    }

    /// <summary>
    /// Central mixer state managing all 3 buses.
    /// </summary>
    public interface IMixerState
    {
        IChannelStrip MusicChannel { get; }
        IChannelStrip RhythmChannel { get; }
        IChannelStrip VoiceChannel { get; }

        PlaybackMode CurrentMode { get; }
        Strength CurrentStrength { get; }
        double CurrentBpm { get; }

        /// <summary>
        /// Master output provider for NAudio.
        /// </summary>
        ISampleProvider MasterOutput { get; }

        /// <summary>
        /// Set mode — delegates to TransitionEngine for smooth change.
        /// </summary>
        void SetMode(PlaybackMode mode, bool smooth = true);

        /// <summary>
        /// Set strength — delegates to TransitionEngine.
        /// </summary>
        void SetStrength(Strength strength, bool smooth = true);
    }
}
