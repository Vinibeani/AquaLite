using System;
namespace AquaLite {
    public interface IDisplayEngine:IDisposable {
        void Update(Settings value);
        void SetConnection(bool enabled);
        bool Wanted { get; }
        EngineState Snapshot(bool includeBitmap);
    }
}
