using System.Collections.Generic;

namespace MovementRecorder.Models
{
    public class MovementJson
    {
        public List<string> objectNames { get; set; }
        public List<Scale> objectScales { get; set; }
        public List<Record> records { get; set; }
    }
    public class Record
    {
        public float songTIme { get; set; }
        public List<float> posX { get; set; }
        public List<float> posY { get; set; }
        public List<float> posZ { get; set; }
        public List<float> rotX { get; set; }
        public List<float> rotY { get; set; }
        public List<float> rotZ { get; set; }
        public List<float> rotW { get; set; }
    }
    public class Scale
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }
}
