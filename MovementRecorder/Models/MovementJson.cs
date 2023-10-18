using System.Collections.Generic;

namespace MovementRecorder.Models
{
    public class MovementJson
    {
        public List<Setting> Settings { get; set; }
        public int recordFrameRate { get; set; }
        public List<string> objectNames { get; set; }
        public List<Scale> objectScales { get; set; }
        public List<Record> records { get; set; }
    }
    public class Setting
    {
        public virtual string name { get; set; }
        public virtual string type { get; set; }
        public virtual List<string> topObjectStrings { get; set; }
        public virtual string rescaleString { get; set; }
        public virtual List<string> searchStirngs { get; set; }
        public virtual List<string> exclusionStrings { get; set; }
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
    public class ResearchJson
    {
        public bool worldSpace { get; set; }
        public List<string> motionEnabled { get; set; }
        public List<MotionScales> otherOneScales { get; set; }
    }
    public class MotionScales
    {
        public string objectName { get; set; }
        public string scale { get; set; }
    }
}
