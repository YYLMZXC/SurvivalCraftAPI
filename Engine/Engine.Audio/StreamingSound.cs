using System.Runtime.InteropServices;
using Engine.Media;
#if BROWSER
using System.Collections.Concurrent;
using Engine.Browser;
using SourceInteger = Engine.Browser.AL.SourceInteger;
using GetSourceInteger = Engine.Browser.AL.GetSourceInteger;
using BufferFormat = Engine.Browser.AL.BufferFormat;
using SourceVector3 = Engine.Browser.AL.SourceVector3;
using SourceState = Engine.Browser.AL.SourceState;
#else
using Silk.NET.OpenAL;
#endif

namespace Engine.Audio {
    public class StreamingSound : BaseSound {
#if BROWSER
        bool m_initialized;
        uint[] m_buffers;
        readonly List<uint> m_freeBuffers = new();
        byte[] m_streamBuffer;

        static readonly ConcurrentDictionary<StreamingSound, bool> toUpdate = new();

        public static void AfterFrame() {
            HashSet<StreamingSound> toRemove = new();
            foreach (StreamingSound streamingSound in toUpdate.Keys) {
                if (streamingSound.State == SoundState.Playing) {
                    streamingSound.UpdateStreaming();
                    if (streamingSound.State != SoundState.Playing) {
                        toRemove.Add(streamingSound);
                    }
                }
                else {
                    toRemove.Add(streamingSound);
                }
            }
            foreach (StreamingSound streamingSound in toRemove) {
                toUpdate.TryRemove(streamingSound, out _);
            }
        }
#else
        Task m_task;
        ManualResetEvent m_stopTaskEvent = new(false);
#endif

        bool m_noMoreData;
        public readonly float m_bufferDuration;

        public StreamingSource StreamingSource { get; set; }

        public int ReadStreamingSource(byte[] buffer, int count) {
            int num = 0;
            if (StreamingSource.BytesCount > 0) {
                while (count > 0) {
                    int num2 = StreamingSource.Read(buffer, num, count);
                    if (num2 > 0) {
                        num += num2;
                        count -= num2;
                        continue;
                    }
                    if (!m_isLooped) {
                        break;
                    }
                    StreamingSource.Position = 0L;
                }
            }
            return num;
        }

        void VerifyStreamingSource(StreamingSource streamingSource) {
            ArgumentNullException.ThrowIfNull(streamingSource);
            if (streamingSource.ChannelsCount < 1
                || streamingSource.ChannelsCount > 2) {
                throw new InvalidOperationException("Unsupported channels count.");
            }
            if (streamingSource.SamplingFrequency < 8000
                || streamingSource.SamplingFrequency > 192000) {
                throw new InvalidOperationException("Unsupported frequency.");
            }
        }

        public StreamingSound(StreamingSource streamingSource,
            float volume = 1f,
            float pitch = 1f,
            float pan = 0f,
            bool isLooped = false,
            bool disposeOnStop = false,
            float bufferDuration = 0.3f) {
            VerifyStreamingSource(streamingSource);
            StreamingSource = streamingSource;
            ChannelsCount = streamingSource.ChannelsCount;
            SamplingFrequency = streamingSource.SamplingFrequency;
            Volume = volume;
            Pitch = pitch;
            Pan = pan;
            IsLooped = isLooped;
            DisposeOnStop = disposeOnStop;
            m_bufferDuration = Math.Clamp(bufferDuration, 0.01f, 10f);
#if !BROWSER
            if (m_source == 0) {
                return;
            }
            m_task = Task.Run(
                delegate {
                    try {
                        StreamingThreadFunction();
                    }
                    catch (Exception message) {
                        Log.Error(message);
                    }
                }
            );
#endif
        }

        internal override void InternalPlay(Vector3 direction) {
            if (m_source != 0) {
                toUpdate.TryAdd(this, false);
                uint source = (uint)m_source;
                Mixer.AL.SetSourceProperty(source, SourceVector3.Position, direction.X, direction.Y, direction.Z);
                Mixer.AL.SourcePlay(source);
                Mixer.CheckALError();
            }
        }

        internal override void InternalPause() {
            if (m_source != 0) {
                Mixer.AL.SourcePause((uint)m_source);
                Mixer.CheckALError();
            }
        }

        internal override void InternalStop() {
            if (m_source != 0) {
                Mixer.AL.SourceStop((uint)m_source);
                Mixer.CheckALError();
                StreamingSource.Position = 0L;
#if BROWSER
                m_noMoreData = false;
#else
                lock (m_lock) {
                    m_noMoreData = false;
                }
#endif
            }
        }

        internal override void InternalDispose() {
#if BROWSER
            if (m_source != 0) {
                uint source = (uint)m_source;
                Mixer.AL.SourceStop(source);
                Mixer.AL.SetSourceProperty(source, SourceInteger.Buffer, 0);
                Mixer.CheckALError();
            }
            if (m_buffers != null) {
                foreach (uint b in m_buffers) {
                    if (b != 0) {
                        uint buffer = b;
                        Mixer.AL.DeleteBuffer(buffer);
                        Mixer.CheckALError();
                    }
                }
                m_buffers = null;
                m_freeBuffers.Clear();
            }
#else
            if (m_stopTaskEvent != null
                && m_task != null) {
                m_stopTaskEvent.Set();
                m_task.Wait();
                m_task = null;
                m_stopTaskEvent.Dispose();
                m_stopTaskEvent = null;
            }
#endif
            if (StreamingSource != null) {
                StreamingSource.Dispose();
                StreamingSource = null;
            }
            base.InternalDispose();
        }

#if BROWSER
        void InitializeStreaming() {
            if (m_initialized || m_source == 0) {
                return;
            }
            m_buffers = new uint[3];
            int samplesPerBuffer = (int)(SamplingFrequency * m_bufferDuration / m_buffers.Length);
            m_streamBuffer = new byte[2 * ChannelsCount * samplesPerBuffer];
            try {
                for (int i = 0; i < m_buffers.Length; i++) {
                    uint buffer = Mixer.AL.GenBuffer();
                    Mixer.CheckALError();
                    m_buffers[i] = buffer;
                    m_freeBuffers.Add(buffer);
                }

                m_initialized = true;
            }
            catch (Exception ex) {
                Log.Error(ex);
            }
        }

        /// <summary>
        /// Call this ONCE PER FRAME while the sound exists.
        /// </summary>
        public void UpdateStreaming() {
            if (m_source == 0) {
                return;
            }
            InitializeStreaming();
            uint source = (uint)m_source;
            try {
                // Unqueue processed buffers
                Mixer.AL.GetSourceProperty(source, GetSourceInteger.BuffersProcessed, out int processed);
                Mixer.CheckALError();
                for (int i = 0; i < processed; i++) {
                    unsafe {
                        uint buffer;
                        Mixer.AL.SourceUnqueueBuffers(source, 1, &buffer);
                        Mixer.CheckALError();
                        m_freeBuffers.Add(buffer);
                    }
                }
                // Queue new data
                while (m_freeBuffers.Count > 0
                    && !m_noMoreData
                    && State == SoundState.Playing) {
                    int bytesRead = ReadStreamingSource(m_streamBuffer, m_streamBuffer.Length);
                    m_noMoreData = bytesRead < m_streamBuffer.Length;
                    if (bytesRead <= 0) {
                        break;
                    }
                    uint buffer = m_freeBuffers[^1];
                    m_freeBuffers.RemoveAt(m_freeBuffers.Count - 1);
                    GCHandle handle = GCHandle.Alloc(m_streamBuffer, GCHandleType.Pinned);
                    unsafe {
                        Mixer.AL.BufferData(buffer, ChannelsCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, handle.AddrOfPinnedObject().ToPointer(), bytesRead, SamplingFrequency);
                    }
                    Mixer.CheckALError();
                    handle.Free();
                    unsafe {
                        Mixer.AL.SourceQueueBuffers(source, 1, &buffer);
                    }
                    Mixer.CheckALError();
                }
                // Ensure playback continues
                Mixer.AL.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);
                Mixer.CheckALError();
                if (state != (int)SourceState.Playing
                    && State == SoundState.Playing) {
                    Mixer.AL.SourcePlay(source);
                    Mixer.CheckALError();
                }
                // End of stream
                if (m_noMoreData && state == (int)SourceState.Stopped) {
                    Stop();
                }
            }
            catch (Exception ex) {
                Log.Error(ex);
                Stop();
            }
        }
#else
        unsafe void StreamingThreadFunction() {
            uint[] array = new uint[3];
            List<uint> list = new();
            int millisecondsTimeout = Math.Clamp((int)(0.5f * m_bufferDuration / array.Length * 1000f), 1, 100);
            byte[] array2 = new byte[2 * ChannelsCount * (int)(SamplingFrequency * m_bufferDuration / array.Length)];
            for (int i = 0; i < array.Length; i++) {
                uint num = Mixer.AL.GenBuffer();
                Mixer.CheckALError();
                array[i] = num;
                list.Add(num);
            }
            uint source = (uint)m_source;
            do {
                lock (m_lock) {
                    if (!m_noMoreData) {
                        Mixer.AL.GetSourceProperty(source, GetSourceInteger.BuffersProcessed, out int value);
                        Mixer.CheckALError();
                        for (int j = 0; j < value; j++) {
                            uint item = 0u;
                            Mixer.AL.SourceUnqueueBuffers(source, 1, &item);
                            Mixer.CheckALError();
                            list.Add(item);
                        }
                        if (list.Count > 0
                            && !m_noMoreData
                            && State == SoundState.Playing) {
                            int num2 = ReadStreamingSource(array2, array2.Length);
                            m_noMoreData = num2 < array2.Length;
                            if (num2 > 0) {
                                uint num3 = list[^1];
                                GCHandle gCHandle = GCHandle.Alloc(array2, GCHandleType.Pinned);
                                Mixer.AL.BufferData(
                                    num3,
                                    ChannelsCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                                    gCHandle.AddrOfPinnedObject().ToPointer(),
                                    num2,
                                    SamplingFrequency
                                );
                                Mixer.CheckALError();
                                Mixer.AL.SourceQueueBuffers(source, 1, &num3);
                                Mixer.CheckALError();
                                list.RemoveAt(list.Count - 1);
                                Mixer.AL.GetSourceProperty(source, GetSourceInteger.SourceState, out int sourceState);
                                Mixer.CheckALError();
                                if (sourceState != (int)SourceState.Playing) {
                                    Mixer.AL.SourcePlay(source);
                                    Mixer.CheckALError();
                                }
                            }
                        }
                    }
                    else {
                        Mixer.AL.GetSourceProperty(source, GetSourceInteger.SourceState, out int sourceState);
                        if (sourceState == (int)SourceState.Stopped) {
                            Dispatcher.Dispatch(delegate { Stop(); });
                        }
                    }
                }
            }
            while (!m_stopTaskEvent.WaitOne(millisecondsTimeout));
            Mixer.AL.SourceStop(source);
            Mixer.CheckALError();
            Mixer.AL.SetSourceProperty(source, SourceInteger.Buffer, 0);
            Mixer.CheckALError();
            for (int k = 0; k < array.Length; k++) {
                if (array[k] != 0) {
                    Mixer.AL.DeleteBuffer(array[k]);
                    Mixer.CheckALError();
                    array[k] = 0;
                }
            }
        }
#endif
    }
}