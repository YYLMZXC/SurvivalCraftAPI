using Silk.NET.OpenAL;

namespace Engine.Browser {
    public class AL {
        public uint GenSource() {
            OAL.alGenSources(1, out uint source);
            return source;
        }

        public void DeleteSource(uint source) => OAL.alDeleteSources(1, ref source);
        public void GetSourceProperty(uint source, GetSourceInteger param, out int value) => OAL.alGetSourcei(source, (int)param, out value);

        public void SetSourceProperty(uint source, SourceFloat param, float value) => OAL.alSourcef(source, (int)param, value);

        public void SetSourceProperty(uint source, SourceVector3 param, float value1, float value2, float value3) =>
            OAL.alSource3f(source, (int)param, value1, value2, value3);

        public void SetSourceProperty(uint source, SourceInteger param, int value) => OAL.alSourcei(source, (int)param, value);

        public void SetSourceProperty(uint source, SourceBoolean param, bool value) => OAL.alSourcei(source, (int)param, value ? 1 : 0);

        public void SourcePlay(uint source) => OAL.alSourcePlay(source);

        public void SourcePause(uint source) => OAL.alSourcePause(source);

        public void SourceStop(uint source) => OAL.alSourceStop(source);

        public void SourceRewind(uint source) => OAL.alSourceRewind(source);

        public uint GenBuffer() {
            OAL.alGenBuffers(1, out uint buffer);
            return buffer;
        }

        public void DeleteBuffer(uint buffer) => OAL.alDeleteBuffers(1, ref buffer);

        public unsafe void BufferData(uint buffer, BufferFormat format, void* data, int size, int frequency) =>
            OAL.alBufferData(buffer, (int)format, data, size, frequency);

        public unsafe void SourceUnqueueBuffers(uint source, int count, uint* buffers) => OAL.alSourceUnqueueBuffers(source, count, buffers);

        public unsafe void SourceQueueBuffers(uint source, int count, uint* buffers) => OAL.alSourceQueueBuffers(source, count, buffers);
        public void SetListenerProperty(ListenerFloat param, float value) => OAL.alListenerf((int)param, value);

        public void DistanceModel(DistanceModel model) => OAL.alDistanceModel((int)model);
        public AudioError GetError() => (AudioError)OAL.alGetError();
    }
}