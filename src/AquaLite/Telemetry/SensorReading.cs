using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AquaLite {
    [DataContract]
    public sealed class SensorReading {
        [DataMember] public string Id, Group, Name, Unit, Source, Hardware, Kind;
        [DataMember] public int Index;
        [DataMember] public double? Value;
        [DataMember] public DateTime Time;
        public static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
    [DataContract]
    public sealed class SensorSnapshot {
        [DataMember] public int Version = 3;
        [DataMember] public DateTime TimeUtc;
        [DataMember] public string Status = "Leitor de sensores não ativado";
        [DataMember] public List<SensorReading> Sensors = new List<SensorReading>();
    }
}
