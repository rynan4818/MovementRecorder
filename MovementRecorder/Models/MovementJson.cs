using System.Collections.Generic;

namespace MovementRecorder.Models
{
    public class MovementJson
    {
        public List<Record> record { get; set; }
        public List<string> avatarObjectNames { get; set; }
        public List<float> avatarScale { get; set; }
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
}
